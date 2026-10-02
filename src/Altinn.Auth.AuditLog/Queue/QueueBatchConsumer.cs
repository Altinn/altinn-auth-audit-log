using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Queue;
using System.Diagnostics;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Outcome of processing one received batch.
/// </summary>
public enum BatchOutcome
{
    /// <summary>All decodable messages were committed and deleted.</summary>
    Committed,

    /// <summary>Nothing was persisted because every message was poisoned (or the batch was empty).</summary>
    NothingToPersist,

    /// <summary>The batch failed with a transient error; messages were left on the queue for redelivery.</summary>
    TransientFailure,

    /// <summary>The batch was rejected because of message data and was processed message by message (see counters for details).</summary>
    FallbackPerMessage,

    /// <summary>The batch failed because of a system problem (schema, privileges, configuration); messages were left on the queue and the circuit breaker was notified.</summary>
    SystemicFailure,
}

/// <summary>
/// Consumes a Storage Queue in batches: receive up to 32 messages, decode them in-process, persist the
/// batch in one transaction, and only then delete the messages. Replaces the Azure Functions queue trigger
/// plus HTTP hop (see issue #335).
/// </summary>
/// <remarks>
/// Delivery is at-least-once with batch granularity. Retry design:
/// <list type="bullet">
///   <item><b>Transient persist failure</b> (DB down, timeout, missing partition): roll back, retry in-process with
///   exponential backoff, then leave the messages on the queue; they reappear after the visibility timeout.
///   Repeated transient failures open a circuit breaker that pauses receiving.</item>
///   <item><b>Data error of a single message</b> (undecodable, fails validation, rejected by a DB constraint): the message
///   is copied to the poison queue and deleted. A data error on a whole batch triggers per-message fallback to
///   find the offending message(s).</item>
///   <item><b>Systemic failure</b> (schema mismatch, insufficient privilege, unknown exception): nothing is poisoned;
///   the messages stay on the queue and the circuit breaker pauses consumption.</item>
///   <item><b>Repeated failure</b>: a message dequeued more than <see cref="QueueSettings.MaxDequeueCount"/> times is poisoned
///   regardless of cause.</item>
///   <item><b>Failure after commit</b> (delete fails, pod killed): the message is redelivered and becomes a duplicate row;
///   mitigated by a generous visibility timeout and (future) dedup.</item>
/// </list>
/// </remarks>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed class QueueBatchConsumer<TEvent> : BackgroundService
{
    private readonly IRawQueue _queue;
    private readonly IQueueEventProcessor<TEvent> _processor;
    private readonly QueueSettings _settings;
    private readonly TimeSpan _depthSampleInterval;
    private readonly bool _createQueuesIfNotExists;
    private readonly QueueConsumerMetrics _metrics;
    private readonly QueueConsumerHealthState _health;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly CircuitBreaker _circuit;
    private readonly CancellationTokenSource _gracefulCts = new();

    public QueueBatchConsumer(
        IRawQueue queue,
        IQueueEventProcessor<TEvent> processor,
        QueueSettings settings,
        TimeSpan depthSampleInterval,
        bool createQueuesIfNotExists,
        QueueConsumerMetrics metrics,
        QueueConsumerHealthState health,
        TimeProvider timeProvider,
        ILogger<QueueBatchConsumer<TEvent>> logger)
    {
        _queue = queue;
        _processor = processor;
        _settings = settings;
        _depthSampleInterval = depthSampleInterval;
        _createQueuesIfNotExists = createQueuesIfNotExists;
        _metrics = metrics;
        _health = health;
        _timeProvider = timeProvider;
        _logger = logger;
        _circuit = new CircuitBreaker(settings.CircuitBreakFailuresBeforeOpen, settings.CircuitBreakOpenDuration, timeProvider);
    }

    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public string QueueName => _queue.Name;

    /// <summary>
    /// Gets the circuit breaker (exposed for tests).
    /// </summary>
    internal CircuitBreaker Circuit => _circuit;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        QueueConsumerLog.ConsumerStarting(_logger, _queue.Name, _processor.Kind, _settings.Concurrency, _settings.BatchSize, _settings.VisibilityTimeout);

        // Receiving stops immediately on shutdown, but an in-flight batch gets a grace period to commit and delete.
        using var registration = stoppingToken.Register(() => _gracefulCts.CancelAfter(_settings.ShutdownGracePeriod));

        _health.Register(_queue.Name, _timeProvider.GetUtcNow());

        try
        {
            if (_createQueuesIfNotExists)
            {
                // Opt-in: needs queues/write (Storage Queue Data Contributor), which the message-level roles do not grant.
                await EnsureQueuesAsync(stoppingToken);
            }

            var tasks = new List<Task>(_settings.Concurrency + 1);
            for (var i = 0; i < _settings.Concurrency; i++)
            {
                tasks.Add(RunWorkerAsync(i, stoppingToken));
            }

            tasks.Add(SampleDepthAsync(stoppingToken));

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
        finally
        {
            QueueConsumerLog.ConsumerStopped(_logger, _queue.Name);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _gracefulCts.Dispose();
        base.Dispose();
    }

    private async Task EnsureQueuesAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.EnsureExistsAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                QueueConsumerLog.EnsureQueueFailed(_logger, ex, _queue.Name, _settings.ReceiveFailureBackoff);
                await Task.Delay(_settings.ReceiveFailureBackoff, _timeProvider, stoppingToken);
            }
        }
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_circuit.IsOpen(out var remaining))
                {
                    await Task.Delay(remaining, _timeProvider, stoppingToken);
                    continue;
                }

                IReadOnlyList<RawQueueMessage> messages;
                try
                {
                    messages = await _queue.ReceiveAsync(_settings.BatchSize, _settings.VisibilityTimeout, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _metrics.ReceiveFailed(_queue.Name);
                    QueueConsumerLog.ReceiveFailed(_logger, ex, _queue.Name, _settings.ReceiveFailureBackoff);
                    await Task.Delay(_settings.ReceiveFailureBackoff, _timeProvider, stoppingToken);
                    continue;
                }

                _health.ReportReceive(_queue.Name, _timeProvider.GetUtcNow());

                if (messages.Count == 0)
                {
                    await Task.Delay(_settings.EmptyQueueBackoff, _timeProvider, stoppingToken);
                    continue;
                }

                await ProcessBatchAsync(messages, _gracefulCts.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                QueueConsumerLog.WorkerFailed(_logger, ex, workerId, _queue.Name, _settings.ReceiveFailureBackoff);
                try
                {
                    await Task.Delay(_settings.ReceiveFailureBackoff, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task SampleDepthAsync(CancellationToken stoppingToken)
    {
        if (_depthSampleInterval <= TimeSpan.Zero)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var depth = await _queue.GetApproximateMessageCountAsync(stoppingToken);
                if (depth.HasValue)
                {
                    _metrics.RecordDepth(_queue.Name, depth.Value);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                QueueConsumerLog.DepthSampleFailed(_logger, ex, _queue.Name);
            }

            try
            {
                await Task.Delay(_depthSampleInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes one received batch. Internal so it can be unit tested without the receive loop.
    /// </summary>
    internal async Task<BatchOutcome> ProcessBatchAsync(IReadOnlyList<RawQueueMessage> messages, CancellationToken cancellationToken)
    {
        var start = _timeProvider.GetTimestamp();
        var queue = _queue.Name;
        _metrics.MessagesReceived(queue, messages.Count);
        RecordOldestMessageAge(messages);

        // 1. Triage: poison messages that are permanently broken or have been retried too often.
        var decoded = new List<(RawQueueMessage Message, TEvent Event)>(messages.Count);
        foreach (var message in messages)
        {
            if (message.DequeueCount > _settings.MaxDequeueCount)
            {
                QueueConsumerLog.MaxDequeueCountExceeded(_logger, message.MessageId, queue, message.DequeueCount, _settings.MaxDequeueCount);
                await PoisonAsync(message, "max_dequeue_count_exceeded", cancellationToken);
                continue;
            }

            try
            {
                decoded.Add((message, _processor.Decode(message.Body.ToMemory())));
            }
            catch (MessageDecodeException ex)
            {
                QueueConsumerLog.MessageUndecodable(_logger, ex, message.MessageId, queue, ex.Reason);
                await PoisonAsync(message, ex.Reason, cancellationToken);
            }
        }

        if (decoded.Count == 0)
        {
            return Complete(BatchOutcome.NothingToPersist, start);
        }

        // 2. Persist the whole batch in one transaction, retrying transient failures in-process.
        var events = new TEvent[decoded.Count];
        for (var i = 0; i < decoded.Count; i++)
        {
            events[i] = decoded[i].Event;
        }

        var result = await PersistWithRetryAsync(events, cancellationToken);
        switch (result.Kind)
        {
            case PersistResultKind.Committed:
                await DeleteAllAsync(decoded, cancellationToken);
                _circuit.RecordSuccess();
                var outcome = Complete(BatchOutcome.Committed, start);
                QueueConsumerLog.BatchCommitted(_logger, decoded.Count, queue, _timeProvider.GetElapsedTime(start));
                return outcome;

            case PersistResultKind.Transient:
                // Leave every message on the queue; it becomes visible again after the visibility timeout.
                QueueConsumerLog.TransientPersistExhausted(_logger, result.Exception!, decoded.Count, queue);
                RecordTransientFailure();
                return Complete(BatchOutcome.TransientFailure, start);

            case PersistResultKind.DataError:
                QueueConsumerLog.DataErrorBatchFailure(_logger, result.Exception!, decoded.Count, queue);
                await FallbackPerMessageAsync(decoded, cancellationToken);
                return Complete(BatchOutcome.FallbackPerMessage, start);

            case PersistResultKind.Systemic:
                // Schema, privilege or programming error: every message would fail the same way. Poisoning would
                // delete valid audit events from the source queue, so leave them and pause via the circuit breaker.
                QueueConsumerLog.SystemicBatchFailure(_logger, result.Exception!, decoded.Count, queue);
                RecordTransientFailure();
                return Complete(BatchOutcome.SystemicFailure, start);

            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    /// A batch was rejected because of message data: persist each message on its own so a single bad row does not
    /// block the others. Only a message that fails with a data error on its own is poisoned.
    /// </summary>
    private async Task FallbackPerMessageAsync(List<(RawQueueMessage Message, TEvent Event)> decoded, CancellationToken cancellationToken)
    {
        var anyLeftOnQueue = false;
        var anyCommitted = false;

        foreach (var (message, @event) in decoded)
        {
            var result = await PersistWithRetryAsync([@event], cancellationToken);
            switch (result.Kind)
            {
                case PersistResultKind.Committed:
                    await DeleteAsync(message, cancellationToken);
                    anyCommitted = true;
                    break;

                case PersistResultKind.DataError:
                    QueueConsumerLog.DataErrorMessageFailure(_logger, result.Exception!, message.MessageId, _queue.Name);
                    await PoisonAsync(message, "persist_failed", cancellationToken);
                    break;

                case PersistResultKind.Systemic:
                    QueueConsumerLog.SystemicBatchFailure(_logger, result.Exception!, 1, _queue.Name);
                    anyLeftOnQueue = true;
                    break;

                case PersistResultKind.Transient:
                    // Leave it; redelivered after the visibility timeout.
                    anyLeftOnQueue = true;
                    break;
            }
        }

        if (anyLeftOnQueue)
        {
            RecordTransientFailure();
        }
        else if (anyCommitted)
        {
            _circuit.RecordSuccess();
        }
    }

    private async Task<PersistResult> PersistWithRetryAsync(IReadOnlyList<TEvent> events, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _settings.TransientRetryAttempts);
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await _processor.PersistAsync(events, cancellationToken);
                _metrics.MessagesPersisted(_queue.Name, events.Count);
                return PersistResult.Committed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                switch (PersistFailureClassifier.Classify(ex))
                {
                    case PersistFailureClass.DataError:
                        return PersistResult.DataError(ex);

                    case PersistFailureClass.Systemic:
                        return PersistResult.Systemic(ex);
                }

                last = ex;
                if (attempt < attempts)
                {
                    var delay = BackoffDelay(attempt);
                    QueueConsumerLog.TransientPersistFailure(_logger, ex, events.Count, _queue.Name, attempt, attempts, delay);
                    await Task.Delay(delay, _timeProvider, cancellationToken);
                }
            }
        }

        return PersistResult.Transient(last!);
    }

    private TimeSpan BackoffDelay(int attempt)
    {
        var baseDelay = _settings.TransientRetryBaseDelay;
        if (baseDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var factor = Math.Pow(2, attempt - 1);
        var jitter = 1 + (Random.Shared.NextDouble() * 0.2); // 0-20 % jitter
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor * jitter);
    }

    private void RecordTransientFailure()
    {
        if (_circuit.RecordFailure())
        {
            _metrics.CircuitOpened(_queue.Name);
            QueueConsumerLog.CircuitOpened(_logger, _queue.Name, _settings.CircuitBreakOpenDuration);
        }
    }

    private async Task DeleteAllAsync(List<(RawQueueMessage Message, TEvent Event)> decoded, CancellationToken cancellationToken)
    {
        var deletes = new Task[decoded.Count];
        for (var i = 0; i < decoded.Count; i++)
        {
            deletes[i] = DeleteAsync(decoded[i].Message, cancellationToken);
        }

        await Task.WhenAll(deletes);
    }

    private async Task DeleteAsync(RawQueueMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _queue.DeleteAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Already committed: the message will be redelivered and produce a duplicate row.
            _metrics.DeleteFailed(_queue.Name);
            QueueConsumerLog.DeleteFailed(_logger, ex, message.MessageId, _queue.Name);
        }
    }

    private async Task PoisonAsync(RawQueueMessage message, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _queue.SendToPoisonAsync(message, cancellationToken);
            await _queue.DeleteAsync(message, cancellationToken);
            _metrics.MessagePoisoned(_queue.Name, reason);
            QueueConsumerLog.MessagePoisoned(_logger, message.MessageId, _queue.Name, reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Not deleted from the source queue: it comes back and we try to poison it again.
            _metrics.PoisonFailed(_queue.Name);
            QueueConsumerLog.PoisonFailed(_logger, ex, message.MessageId, _queue.Name);
        }
    }

    private void RecordOldestMessageAge(IReadOnlyList<RawQueueMessage> messages)
    {
        DateTimeOffset? oldest = null;
        foreach (var message in messages)
        {
            if (message.InsertedOn is { } insertedOn && (oldest is null || insertedOn < oldest))
            {
                oldest = insertedOn;
            }
        }

        if (oldest is { } value)
        {
            _metrics.RecordOldestMessageAge(_queue.Name, _timeProvider.GetUtcNow() - value);
        }
    }

    private BatchOutcome Complete(BatchOutcome outcome, long start)
    {
        _metrics.BatchCompleted(_queue.Name, OutcomeTag(outcome), _timeProvider.GetElapsedTime(start));
        return outcome;
    }

    private static string OutcomeTag(BatchOutcome outcome)
        => outcome switch
        {
            BatchOutcome.Committed => "committed",
            BatchOutcome.NothingToPersist => "nothing_to_persist",
            BatchOutcome.TransientFailure => "transient_failure",
            BatchOutcome.FallbackPerMessage => "fallback_per_message",
            BatchOutcome.SystemicFailure => "systemic_failure",
            _ => "unknown",
        };

    private enum PersistResultKind
    {
        Committed,
        Transient,
        DataError,
        Systemic,
    }

    private readonly record struct PersistResult(PersistResultKind Kind, Exception? Exception)
    {
        public static PersistResult Committed => new(PersistResultKind.Committed, null);

        public static PersistResult Transient(Exception exception) => new(PersistResultKind.Transient, exception);

        public static PersistResult DataError(Exception exception) => new(PersistResultKind.DataError, exception);

        public static PersistResult Systemic(Exception exception) => new(PersistResultKind.Systemic, exception);
    }
}
