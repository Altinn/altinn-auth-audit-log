using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// OpenTelemetry metrics for the queue consumers (meter <see cref="MeterName"/>).
/// </summary>
public sealed class QueueConsumerMetrics
{
    /// <summary>
    /// The meter name.
    /// </summary>
    public const string MeterName = "Altinn.Auth.AuditLog";

    private const string QueueTag = "queue";
    private const string OutcomeTag = "outcome";
    private const string ReasonTag = "reason";

    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _messagesPersisted;
    private readonly Counter<long> _messagesPoisoned;
    private readonly Counter<long> _poisonFailed;
    private readonly Counter<long> _deleteFailed;
    private readonly Counter<long> _receiveFailed;
    private readonly Counter<long> _batches;
    private readonly Counter<long> _circuitOpened;
    private readonly Histogram<int> _batchSize;
    private readonly Histogram<double> _batchDuration;

    private readonly ConcurrentDictionary<string, long> _depth = new();
    private readonly ConcurrentDictionary<string, double> _oldestMessageAge = new();

    public QueueConsumerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _messagesReceived = meter.CreateCounter<long>("auditlog.queue.messages.received", unit: "{message}", description: "Messages received from the queue");
        _messagesPersisted = meter.CreateCounter<long>("auditlog.queue.messages.persisted", unit: "{message}", description: "Messages committed to the database and deleted from the queue");
        _messagesPoisoned = meter.CreateCounter<long>("auditlog.queue.messages.poisoned", unit: "{message}", description: "Messages moved to the poison queue");
        _poisonFailed = meter.CreateCounter<long>("auditlog.queue.messages.poison_failed", unit: "{message}", description: "Messages that could not be moved to the poison queue (left on the queue)");
        _deleteFailed = meter.CreateCounter<long>("auditlog.queue.messages.delete_failed", unit: "{message}", description: "Messages committed to the database but not deleted from the queue (will be redelivered)");
        _receiveFailed = meter.CreateCounter<long>("auditlog.queue.receive_failed", unit: "{call}", description: "Failed receive calls against the queue");
        _batches = meter.CreateCounter<long>("auditlog.queue.batches", unit: "{batch}", description: "Processed batches by outcome");
        _circuitOpened = meter.CreateCounter<long>("auditlog.queue.circuit_opened", unit: "{event}", description: "Times the circuit breaker opened");
        _batchSize = meter.CreateHistogram<int>("auditlog.queue.batch.size", unit: "{message}", description: "Number of messages per received batch");
        _batchDuration = meter.CreateHistogram<double>("auditlog.queue.batch.duration", unit: "ms", description: "Time to process a batch, by outcome");

        meter.CreateObservableGauge("auditlog.queue.depth", ObserveDepth, unit: "{message}", description: "Approximate number of messages in the queue");
        meter.CreateObservableGauge("auditlog.queue.oldest_message_age", ObserveOldestMessageAge, unit: "s", description: "Age of the oldest message in the most recently received batch");
    }

    public void MessagesReceived(string queue, int count)
    {
        _messagesReceived.Add(count, new KeyValuePair<string, object?>(QueueTag, queue));
        _batchSize.Record(count, new KeyValuePair<string, object?>(QueueTag, queue));
    }

    public void MessagesPersisted(string queue, int count)
        => _messagesPersisted.Add(count, new KeyValuePair<string, object?>(QueueTag, queue));

    public void MessagePoisoned(string queue, string reason)
        => _messagesPoisoned.Add(1, new KeyValuePair<string, object?>(QueueTag, queue), new KeyValuePair<string, object?>(ReasonTag, reason));

    public void PoisonFailed(string queue)
        => _poisonFailed.Add(1, new KeyValuePair<string, object?>(QueueTag, queue));

    public void DeleteFailed(string queue)
        => _deleteFailed.Add(1, new KeyValuePair<string, object?>(QueueTag, queue));

    public void ReceiveFailed(string queue)
        => _receiveFailed.Add(1, new KeyValuePair<string, object?>(QueueTag, queue));

    public void CircuitOpened(string queue)
        => _circuitOpened.Add(1, new KeyValuePair<string, object?>(QueueTag, queue));

    public void BatchCompleted(string queue, string outcome, TimeSpan duration)
    {
        _batches.Add(1, new KeyValuePair<string, object?>(QueueTag, queue), new KeyValuePair<string, object?>(OutcomeTag, outcome));
        _batchDuration.Record(duration.TotalMilliseconds, new KeyValuePair<string, object?>(QueueTag, queue), new KeyValuePair<string, object?>(OutcomeTag, outcome));
    }

    public void RecordDepth(string queue, long depth)
        => _depth[queue] = depth;

    public void RecordOldestMessageAge(string queue, TimeSpan age)
        => _oldestMessageAge[queue] = Math.Max(0, age.TotalSeconds);

    private IEnumerable<Measurement<long>> ObserveDepth()
    {
        foreach (var (queue, depth) in _depth)
        {
            yield return new Measurement<long>(depth, new KeyValuePair<string, object?>(QueueTag, queue));
        }
    }

    private IEnumerable<Measurement<double>> ObserveOldestMessageAge()
    {
        foreach (var (queue, age) in _oldestMessageAge)
        {
            yield return new Measurement<double>(age, new KeyValuePair<string, object?>(QueueTag, queue));
        }
    }
}
