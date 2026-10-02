namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Source-generated log messages for the queue consumers.
/// </summary>
internal static partial class QueueConsumerLog
{
    [LoggerMessage(1, LogLevel.Information, "Queue consumer for '{Queue}' ({Kind}) starting with {Concurrency} worker(s), batch size {BatchSize}, visibility timeout {VisibilityTimeout}")]
    public static partial void ConsumerStarting(ILogger logger, string queue, string kind, int concurrency, int batchSize, TimeSpan visibilityTimeout);

    [LoggerMessage(2, LogLevel.Information, "Queue consumer for '{Queue}' stopped")]
    public static partial void ConsumerStopped(ILogger logger, string queue);

    [LoggerMessage(3, LogLevel.Warning, "Could not ensure queue '{Queue}' and its poison queue exist; retrying in {Delay}")]
    public static partial void EnsureQueueFailed(ILogger logger, Exception exception, string queue, TimeSpan delay);

    [LoggerMessage(4, LogLevel.Warning, "Receive from queue '{Queue}' failed; backing off {Delay}")]
    public static partial void ReceiveFailed(ILogger logger, Exception exception, string queue, TimeSpan delay);

    [LoggerMessage(5, LogLevel.Error, "Unexpected error in worker {WorkerId} for queue '{Queue}'; backing off {Delay}")]
    public static partial void WorkerFailed(ILogger logger, Exception exception, int workerId, string queue, TimeSpan delay);

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

    [LoggerMessage(12, LogLevel.Error, "Permanent failure persisting batch of {BatchSize} from queue '{Queue}'; falling back to per-message processing to isolate the offending message(s)")]
    public static partial void PermanentBatchFailure(ILogger logger, Exception exception, int batchSize, string queue);

    [LoggerMessage(13, LogLevel.Error, "Message {MessageId} on queue '{Queue}' failed permanently when persisted on its own; moving to poison queue")]
    public static partial void PermanentMessageFailure(ILogger logger, Exception exception, string messageId, string queue);

    [LoggerMessage(14, LogLevel.Warning, "Message {MessageId} on queue '{Queue}' was committed to the database but could not be deleted from the queue; it will be redelivered (duplicate)")]
    public static partial void DeleteFailed(ILogger logger, Exception exception, string messageId, string queue);

    [LoggerMessage(15, LogLevel.Error, "Circuit opened for queue '{Queue}' after repeated transient failures; pausing receive for {Duration}")]
    public static partial void CircuitOpened(ILogger logger, string queue, TimeSpan duration);

    [LoggerMessage(16, LogLevel.Debug, "Batch of {BatchSize} from queue '{Queue}' committed and deleted in {Duration}")]
    public static partial void BatchCommitted(ILogger logger, int batchSize, string queue, TimeSpan duration);

    [LoggerMessage(17, LogLevel.Debug, "Could not sample depth of queue '{Queue}'")]
    public static partial void DepthSampleFailed(ILogger logger, Exception exception, string queue);
}
