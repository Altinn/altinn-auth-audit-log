using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// <see cref="IRawQueue"/> backed by Azure Storage Queues.
/// </summary>
/// <remarks>
/// Both clients must be created with <see cref="QueueMessageEncoding.Base64"/> (see
/// <see cref="QueueConsumerDependencyInjectionExtensions"/>). That matches the default encoding of the
/// Azure Functions queue trigger this consumer replaces, and it means a poisoned message is written to
/// the poison queue byte-for-byte as the producer sent it.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class StorageRawQueue : IRawQueue
{
    /// <summary>
    /// Poison messages are evidence of lost audit events and must never expire on their own.
    /// Azure interprets -1 seconds as "does not expire".
    /// </summary>
    private static readonly TimeSpan _poisonTimeToLive = TimeSpan.FromSeconds(-1);

    private readonly QueueClient _queue;
    private readonly QueueClient _poisonQueue;

    public StorageRawQueue(QueueClient queue, QueueClient poisonQueue)
    {
        _queue = queue;
        _poisonQueue = poisonQueue;
    }

    /// <inheritdoc/>
    public string Name => _queue.Name;

    /// <inheritdoc/>
    public async Task EnsureExistsAsync(CancellationToken cancellationToken)
    {
        await _queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _poisonQueue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ReceiveAsync(int maxMessages, TimeSpan visibilityTimeout, List<RawQueueMessage> destination, CancellationToken cancellationToken)
    {
        QueueMessage[] messages = await _queue.ReceiveMessagesAsync(maxMessages, visibilityTimeout, cancellationToken);
        foreach (var message in messages)
        {
            destination.Add(new RawQueueMessage(message.MessageId, message.PopReceipt, message.Body, message.DequeueCount, message.InsertedOn));
        }

        return messages.Length;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(RawQueueMessage message, CancellationToken cancellationToken)
        => _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

    /// <inheritdoc/>
    public Task SendToPoisonAsync(RawQueueMessage message, CancellationToken cancellationToken)
        => _poisonQueue.SendMessageAsync(message.Body, visibilityTimeout: null, timeToLive: _poisonTimeToLive, cancellationToken);

    /// <inheritdoc/>
    public async Task<long?> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
    {
        QueueProperties properties = await _queue.GetPropertiesAsync(cancellationToken);
        return properties.ApproximateMessagesCount;
    }
}
