namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// A received queue message. <see cref="Body"/> is the payload after queue-level (base64) decoding.
/// </summary>
/// <param name="MessageId">The queue message id.</param>
/// <param name="PopReceipt">The pop receipt needed to delete the message.</param>
/// <param name="Body">The decoded message body.</param>
/// <param name="DequeueCount">How many times the message has been received, including this time.</param>
/// <param name="InsertedOn">When the message was enqueued.</param>
public sealed record RawQueueMessage(
    string MessageId,
    string PopReceipt,
    BinaryData Body,
    long DequeueCount,
    DateTimeOffset? InsertedOn);

/// <summary>
/// Minimal abstraction over a queue with a poison queue, so the consumer can be tested without Azure.
/// </summary>
public interface IRawQueue
{
    /// <summary>
    /// Gets the queue name (used for logging and metric tags).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates the queue and its poison queue if they do not exist.
    /// </summary>
    Task EnsureExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Receives up to <paramref name="maxMessages"/> messages, making them invisible for <paramref name="visibilityTimeout"/>,
    /// and appends them to <paramref name="destination"/>.
    /// </summary>
    /// <returns>The number of messages received.</returns>
    Task<int> ReceiveAsync(int maxMessages, TimeSpan visibilityTimeout, List<RawQueueMessage> destination, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a message. Only called after the message has been durably persisted (or moved to poison).
    /// </summary>
    Task DeleteAsync(RawQueueMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the message (verbatim body) to the poison queue. The caller deletes it from the source queue afterwards.
    /// </summary>
    Task SendToPoisonAsync(RawQueueMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the approximate number of messages in the queue, if supported.
    /// </summary>
    Task<long?> GetApproximateMessageCountAsync(CancellationToken cancellationToken);
}
