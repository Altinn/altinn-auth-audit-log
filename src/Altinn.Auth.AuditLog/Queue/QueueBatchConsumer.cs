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
/// <para>
/// A single sequential loop per queue: receive a batch, process it to completion, repeat. There is no
/// in-process parallelism; throughput scales with the number of replicas, each of which leases its own
/// messages. The event-type specific work (<see cref="IQueueEventProcessor{TEvent}"/>) is resolved from a
/// service scope created per batch.
/// </para>
/// <para>
/// Delivery is at-least-once with batch granularity. Retry design:
/// </para>
/// <list type="bullet">
///   <item><b>Transient persist failure</b> (DB down, timeout, missing partition): roll back, retry in-process with
///   exponential backoff, then leave the messages on the queue; they reappear after the visibility timeout.
///   Repeated failures open a circuit breaker that pauses polling.</item>
///   <item><b>Data error of a single message</b> (undecodable, fails validation, rejected by a DB constraint): the message
///   is copied to the poison queue and deleted. A data error on a whole batch triggers per-message fallback to
///   find the offending message(s).</item>
///   <item><b>Systemic failure</b> (schema mismatch, insufficient privilege, unknown exception): nothing is poisoned;
///   the messages stay on the queue and the circuit breaker pauses polling.</item>
///   <item><b>Repeated failure</b>: a message dequeued more than <see cref="QueueSettings.MaxDequeueCount"/> times is poisoned
///   regardless of cause.</item>
///   <item><b>Failure after commit</b> (delete fails, pod killed): the message is redelivered and becomes a duplicate row;
///   mitigated by a generous visibility timeout and (future) dedup.</item>
/// </list>
/// </remarks>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed partial class QueueBatchConsumer<TEvent> : BackgroundService
{
    private readonly IRawQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QueueSettings _settings;
    private readonly TimeSpan _depthSampleInterval;
    private readonly bool _createQueuesIfNotExists;
    private readonly QueueConsumerMetrics _metrics;
    private readonly QueueConsumerHealthState _health;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly CircuitBreaker _circuit;
    private readonly CancellationTokenSource _gracefulCts = new();
    private long _messagesSinceProgress;
    private long _batchesSinceProgress;

    public QueueBatchConsumer(
        IRawQueue queue,
        IServiceScopeFactory scopeFactory,
        QueueSettings settings,
        TimeSpan depthSampleInterval,
        bool createQueuesIfNotExists,
        QueueConsumerMetrics metrics,
        QueueConsumerHealthState health,
        TimeProvider timeProvider,
        ILogger<QueueBatchConsumer<TEvent>> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.BatchSize, QueueSettings.MaxBatchSize);

        _queue = queue;
        _scopeFactory = scopeFactory;
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
        Log.ConsumerStarting(_logger, _queue.Name, _settings.BatchSize, _settings.VisibilityTimeout);

        // Polling stops immediately on shutdown, but in-flight batches get a grace period to commit and delete.
        using var registration = stoppingToken.Register(() => _gracefulCts.CancelAfter(_settings.ShutdownGracePeriod));

        _health.Register(_queue.Name, _timeProvider.GetUtcNow());

        try
        {
            if (_createQueuesIfNotExists)
            {
                // Opt-in: needs queues/write (Storage Queue Data Contributor), which the message-level roles do not grant.
                await EnsureQueuesAsync(stoppingToken);
            }

            await Task.WhenAll(PollLoopAsync(stoppingToken), SampleDepthAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
        finally
        {
            Log.ConsumerStopped(_logger, _queue.Name);
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
        var backoff = new Backoff(_settings.ReceiveFailureBackoff, _settings.ReceiveFailureMaxBackoff);

        while (true)
        {
            try
            {
                await _queue.EnsureExistsAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = backoff.Next();
                Log.EnsureQueueFailed(_logger, ex, _queue.Name, delay);
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
        }
    }

    /// <summary>
    /// The poll loop: receive a batch, process it to completion, repeat. Strictly sequential, so the single
    /// <see cref="BatchBuffer"/> can be reused for every batch.
    /// </summary>
    private async Task PollLoopAsync(CancellationToken stoppingToken)
    {
        var emptyBackoff = new Backoff(_settings.EmptyQueueBackoff, _settings.EmptyQueueMaxBackoff);
        var failureBackoff = new Backoff(_settings.ReceiveFailureBackoff, _settings.ReceiveFailureMaxBackoff);

        // Owned by this loop only; reused for every batch.
        var buffer = new BatchBuffer(_settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_circuit.IsOpen(out var remaining))
            {
                await Task.Delay(remaining, _timeProvider, stoppingToken);
                continue;
            }

            buffer.Clear();
            int received;

            try
            {
                received = await _queue.ReceiveAsync(_settings.BatchSize, _settings.VisibilityTimeout, buffer.Messages, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _metrics.ReceiveFailed(_queue.Name);
                var delay = failureBackoff.Next();
                Log.ReceiveFailed(_logger, ex, _queue.Name, delay);
                await Task.Delay(delay, _timeProvider, stoppingToken);
                continue;
            }

            failureBackoff.Reset();
            _health.ReportReceive(_queue.Name, _timeProvider.GetUtcNow());

            if (received == 0)
            {
                var delay = emptyBackoff.Next();
                await Task.Delay(delay, _timeProvider, stoppingToken);
                continue;
            }

            emptyBackoff.Reset();

            try
            {
                // Uses the graceful token: on shutdown the batch in progress gets ShutdownGracePeriod to commit and delete.
                await ProcessBatchAsync(buffer, _gracefulCts.Token);
            }
            catch (OperationCanceledException) when (_gracefulCts.IsCancellationRequested)
            {
                // Grace period elapsed; the messages reappear after the visibility timeout.
                break;
            }
            catch (Exception ex)
            {
                Log.BatchFailedUnexpectedly(_logger, ex, _queue.Name);
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
            long? depth = null;
            try
            {
                depth = await _queue.GetApproximateMessageCountAsync(stoppingToken);
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
                Log.DepthSampleFailed(_logger, ex, _queue.Name);
            }

            // Console-visible liveness: one Information line per sample interval while there is work.
            var messages = Interlocked.Exchange(ref _messagesSinceProgress, 0);
            var batches = Interlocked.Exchange(ref _batchesSinceProgress, 0);
            if (batches > 0 || depth > 0)
            {
                Log.Progress(_logger, _queue.Name, messages, batches, _depthSampleInterval, depth, _circuit.ConsecutiveFailures);
            }

            await Task.Delay(_depthSampleInterval, _timeProvider, stoppingToken);
        }
    }

    /// <summary>
    /// Test-only entry point: processes the given, already received messages as one batch, without the poll loop.
    /// Uses its own buffer, so it never shares state with a running consumer.
    /// </summary>
    internal Task<BatchOutcome> ProcessReceivedMessagesForTestingAsync(IReadOnlyList<RawQueueMessage> messages, CancellationToken cancellationToken)
        => ProcessBatchAsync(new BatchBuffer(messages), cancellationToken);

    private async Task<BatchOutcome> ProcessBatchAsync(BatchBuffer buffer, CancellationToken cancellationToken)
    {
        var start = _timeProvider.GetTimestamp();
        var queue = _queue.Name;
        var messages = buffer.Messages;
        var accepted = buffer.Accepted;
        var events = buffer.Events;

        _metrics.MessagesReceived(queue, messages.Count);
        RecordOldestMessageAge(messages);
        Interlocked.Add(ref _messagesSinceProgress, messages.Count);
        Interlocked.Increment(ref _batchesSinceProgress);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IQueueEventProcessor<TEvent>>();

        // 1. Triage: poison messages that are permanently broken or have been retried too often.
        foreach (var message in messages)
        {
            if (message.DequeueCount > _settings.MaxDequeueCount)
            {
                Log.MaxDequeueCountExceeded(_logger, message.MessageId, queue, message.DequeueCount, _settings.MaxDequeueCount);
                await PoisonAsync(message, "max_dequeue_count_exceeded", cancellationToken);
                continue;
            }

            try
            {
                var evt = processor.Decode(message.Body.ToMemory().Span);
                accepted.Add(message);
                events.Add(evt);
            }
            catch (MessageDecodeException ex)
            {
                Log.MessageUndecodable(_logger, ex, message.MessageId, queue, ex.Reason);
                await PoisonAsync(message, ex.Reason, cancellationToken);
            }
        }

        if (events.Count == 0)
        {
            return Complete(BatchOutcome.NothingToPersist, start);
        }

        // 2. Persist the whole batch in one transaction, retrying transient failures in-process.
        var result = await PersistWithRetryAsync(processor, events, cancellationToken);
        switch (result.Kind)
        {
            case PersistResultKind.Committed:
                await DeleteAllAsync(accepted, cancellationToken);
                _circuit.RecordSuccess();
                Log.BatchCommitted(_logger, events.Count, queue, _timeProvider.GetElapsedTime(start));
                return Complete(BatchOutcome.Committed, start);

            case PersistResultKind.Transient:
                // Leave every message on the queue; it becomes visible again after the visibility timeout.
                Log.TransientPersistExhausted(_logger, result.Exception!, events.Count, queue);
                RecordFailure();
                return Complete(BatchOutcome.TransientFailure, start);

            case PersistResultKind.DataError:
                Log.DataErrorBatchFailure(_logger, result.Exception!, events.Count, queue);
                await FallbackPerMessageAsync(processor, buffer, cancellationToken);
                return Complete(BatchOutcome.FallbackPerMessage, start);

            case PersistResultKind.Systemic:
                // Schema, privilege or programming error: every message would fail the same way. Poisoning would
                // delete valid audit events from the source queue, so leave them and pause via the circuit breaker.
                Log.SystemicBatchFailure(_logger, result.Exception!, events.Count, queue);
                RecordFailure();
                return Complete(BatchOutcome.SystemicFailure, start);

            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    /// A batch was rejected because of message data: persist each message on its own so a single bad row does not
    /// block the others. Only a message that fails with a data error on its own is poisoned.
    /// </summary>
    private async Task FallbackPerMessageAsync(IQueueEventProcessor<TEvent> processor, BatchBuffer buffer, CancellationToken cancellationToken)
    {
        var accepted = buffer.Accepted;
        var events = buffer.Events;
        var single = buffer.Single;
        var anyLeftOnQueue = false;
        var anyCommitted = false;

        for (var i = 0; i < accepted.Count; i++)
        {
            var message = accepted[i];
            single.Clear();
            single.Add(events[i]);

            var result = await PersistWithRetryAsync(processor, single, cancellationToken);
            switch (result.Kind)
            {
                case PersistResultKind.Committed:
                    await DeleteAsync(message, cancellationToken);
                    anyCommitted = true;
                    break;

                case PersistResultKind.DataError:
                    Log.DataErrorMessageFailure(_logger, result.Exception!, message.MessageId, _queue.Name);
                    await PoisonAsync(message, "persist_failed", cancellationToken);
                    break;

                case PersistResultKind.Systemic:
                    Log.SystemicBatchFailure(_logger, result.Exception!, 1, _queue.Name);
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
            RecordFailure();
        }
        else if (anyCommitted)
        {
            _circuit.RecordSuccess();
        }
    }

    /// <summary>
    /// In-process retries exist because every queue-level retry costs a dequeue count (towards
    /// <see cref="QueueSettings.MaxDequeueCount"/>) and a full visibility timeout. A few quick attempts absorb
    /// sub-second blips without touching either.
    /// </summary>
    private async Task<PersistResult> PersistWithRetryAsync(IQueueEventProcessor<TEvent> processor, IReadOnlyList<TEvent> events, CancellationToken cancellationToken)
    {
        var attempts = _settings.TransientRetryAttempts;
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await processor.PersistAsync(events, cancellationToken);
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
                    var delay = TransientRetryDelay(attempt);
                    Log.TransientPersistFailure(_logger, ex, events.Count, _queue.Name, attempt, attempts, delay);
                    await Task.Delay(delay, _timeProvider, cancellationToken);
                }
            }
        }

        return PersistResult.Transient(last!);
    }

    /// <summary>
    /// Exponential backoff with 0-20 % jitter: base, 2×base, 4×base, ...
    /// </summary>
    private TimeSpan TransientRetryDelay(int attempt)
    {
        var baseDelay = _settings.TransientRetryBaseDelay;
        if (baseDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var factor = 1 << (attempt - 1);
        var jitter = 1 + (Random.Shared.NextDouble() * 0.2);
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor * jitter);
    }

    private void RecordFailure()
    {
        if (_circuit.RecordFailure())
        {
            _metrics.CircuitOpened(_queue.Name);
            Log.CircuitOpened(_logger, _queue.Name, _settings.CircuitBreakOpenDuration);
        }
    }

    private async Task DeleteAllAsync(List<RawQueueMessage> messages, CancellationToken cancellationToken)
    {
        var deletes = new Task[messages.Count];
        for (var i = 0; i < messages.Count; i++)
        {
            deletes[i] = DeleteAsync(messages[i], cancellationToken);
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
            Log.DeleteFailed(_logger, ex, message.MessageId, _queue.Name);
        }
    }

    private async Task PoisonAsync(RawQueueMessage message, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _queue.SendToPoisonAsync(message, cancellationToken);
            await _queue.DeleteAsync(message, cancellationToken);
            _metrics.MessagePoisoned(_queue.Name, reason);
            Log.MessagePoisoned(_logger, message.MessageId, _queue.Name, reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Not deleted from the source queue: it comes back and we try to poison it again.
            _metrics.PoisonFailed(_queue.Name);
            Log.PoisonFailed(_logger, ex, message.MessageId, _queue.Name);
        }
    }

    private void RecordOldestMessageAge(List<RawQueueMessage> messages)
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

    /// <summary>
    /// Per-batch storage: received messages, the subset that decoded, their events, and a one-element list for
    /// per-message fallback. The poll loop owns exactly one as a local and clears it between batches, so
    /// steady-state batches do not allocate.
    /// </summary>
    private sealed class BatchBuffer(int capacity)
    {
        public BatchBuffer(IReadOnlyList<RawQueueMessage> messages)
            : this(messages.Count)
        {
            Messages.AddRange(messages);
        }

        public List<RawQueueMessage> Messages { get; } = new(capacity);

        public List<RawQueueMessage> Accepted { get; } = new(capacity);

        public List<TEvent> Events { get; } = new(capacity);

        public List<TEvent> Single { get; } = new(1);

        public void Clear()
        {
            Messages.Clear();
            Accepted.Clear();
            Events.Clear();
            Single.Clear();
        }
    }

    /// <summary>
    /// Doubling backoff capped at a maximum; reset when the operation succeeds.
    /// </summary>
    private sealed class Backoff(TimeSpan initial, TimeSpan max)
    {
        private TimeSpan _next = initial;

        public TimeSpan Next()
        {
            var delay = _next;
            var doubled = delay + delay;
            _next = doubled > max || doubled < delay ? max : doubled;
            return delay;
        }

        public void Reset()
            => _next = initial;
    }

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

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Queue consumer for '{Queue}' starting with batch size {BatchSize}, visibility timeout {VisibilityTimeout}")]
        public static partial void ConsumerStarting(ILogger logger, string queue, int batchSize, TimeSpan visibilityTimeout);

        [LoggerMessage(2, LogLevel.Information, "Queue consumer for '{Queue}' stopped")]
        public static partial void ConsumerStopped(ILogger logger, string queue);

        [LoggerMessage(3, LogLevel.Warning, "Could not ensure queue '{Queue}' and its poison queue exist; retrying in {Delay}")]
        public static partial void EnsureQueueFailed(ILogger logger, Exception exception, string queue, TimeSpan delay);

        [LoggerMessage(4, LogLevel.Warning, "Receive from queue '{Queue}' failed; backing off {Delay}")]
        public static partial void ReceiveFailed(ILogger logger, Exception exception, string queue, TimeSpan delay);

        [LoggerMessage(5, LogLevel.Error, "Batch from queue '{Queue}' failed unexpectedly; its messages are left on the queue")]
        public static partial void BatchFailedUnexpectedly(ILogger logger, Exception exception, string queue);

        [LoggerMessage(6, LogLevel.Warning, "Message {MessageId} on queue '{Queue}' could not be decoded ({Reason}); moving to poison queue")]
        public static partial void MessageUndecodable(ILogger logger, Exception exception, string messageId, string queue, string reason);

        [LoggerMessage(7, LogLevel.Warning, "Message {MessageId} on queue '{Queue}' has been dequeued {DequeueCount} times (max {MaxDequeueCount}); moving to poison queue")]
        public static partial void MaxDequeueCountExceeded(ILogger logger, string messageId, string queue, long dequeueCount, int maxDequeueCount);

        [LoggerMessage(8, LogLevel.Error, "Message {MessageId} on queue '{Queue}' moved to poison queue ({Reason})")]
        public static partial void MessagePoisoned(ILogger logger, string messageId, string queue, string reason);

        [LoggerMessage(9, LogLevel.Error, "Failed to move message {MessageId} on queue '{Queue}' to the poison queue; it stays on the queue and will be redelivered")]
        public static partial void PoisonFailed(ILogger logger, Exception exception, string messageId, string queue);

        [LoggerMessage(10, LogLevel.Warning, "Transient failure persisting batch of {BatchSize} from queue '{Queue}' (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}")]
        public static partial void TransientPersistFailure(ILogger logger, Exception exception, int batchSize, string queue, int attempt, int maxAttempts, TimeSpan delay);

        [LoggerMessage(11, LogLevel.Error, "Transient failures exhausted for batch of {BatchSize} from queue '{Queue}'; messages left on the queue and become visible again after the visibility timeout")]
        public static partial void TransientPersistExhausted(ILogger logger, Exception exception, int batchSize, string queue);

        [LoggerMessage(12, LogLevel.Error, "Batch of {BatchSize} from queue '{Queue}' was rejected because of message data; falling back to per-message processing to isolate the offending message(s)")]
        public static partial void DataErrorBatchFailure(ILogger logger, Exception exception, int batchSize, string queue);

        [LoggerMessage(13, LogLevel.Error, "Message {MessageId} on queue '{Queue}' was rejected because of its data when persisted on its own; moving to poison queue")]
        public static partial void DataErrorMessageFailure(ILogger logger, Exception exception, string messageId, string queue);

        [LoggerMessage(14, LogLevel.Warning, "Message {MessageId} on queue '{Queue}' was committed to the database but could not be deleted from the queue; it will be redelivered (duplicate)")]
        public static partial void DeleteFailed(ILogger logger, Exception exception, string messageId, string queue);

        [LoggerMessage(15, LogLevel.Error, "Circuit opened for queue '{Queue}' after repeated failures; pausing polling for {Duration}")]
        public static partial void CircuitOpened(ILogger logger, string queue, TimeSpan duration);

        [LoggerMessage(16, LogLevel.Debug, "Batch of {BatchSize} from queue '{Queue}' committed and deleted in {Duration}")]
        public static partial void BatchCommitted(ILogger logger, int batchSize, string queue, TimeSpan duration);

        [LoggerMessage(17, LogLevel.Debug, "Could not sample depth of queue '{Queue}'")]
        public static partial void DepthSampleFailed(ILogger logger, Exception exception, string queue);

        [LoggerMessage(18, LogLevel.Critical, "Systemic failure persisting batch of {BatchSize} from queue '{Queue}' (schema, privileges or configuration); nothing poisoned, messages left on the queue, pausing polling via circuit breaker")]
        public static partial void SystemicBatchFailure(ILogger logger, Exception exception, int batchSize, string queue);

        [LoggerMessage(19, LogLevel.Information, "Queue '{Queue}': {Messages} messages in {Batches} batches during the last {Interval}; approximate depth {Depth}; consecutive failures {ConsecutiveFailures}")]
        public static partial void Progress(ILogger logger, string queue, long messages, long batches, TimeSpan interval, long? depth, int consecutiveFailures);
    }
}
