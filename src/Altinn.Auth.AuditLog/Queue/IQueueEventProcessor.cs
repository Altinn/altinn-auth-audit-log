using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Queue;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Event-type specific part of a queue consumer: how to decode a message and how to persist a batch.
/// Resolved from a service scope created per batch.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IQueueEventProcessor<TEvent>
{
    /// <summary>
    /// Gets a short name used in logs and metric tags (<c>authorization</c> / <c>authentication</c>).
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Decodes and validates a message body.
    /// </summary>
    /// <exception cref="MessageDecodeException">If the message is permanently undecodable.</exception>
    TEvent Decode(ReadOnlySpan<byte> body);

    /// <summary>
    /// Persists a batch atomically: when this returns, every event is committed; when it throws, none are.
    /// </summary>
    Task PersistAsync(IReadOnlyList<TEvent> events, CancellationToken cancellationToken);
}

/// <summary>
/// Processor for the <c>authorizationeventlog</c> queue.
/// </summary>
internal sealed class AuthorizationQueueEventProcessor : IQueueEventProcessor<AuthorizationEvent>
{
    private readonly IAuthorizationEventService _service;

    public AuthorizationQueueEventProcessor(IAuthorizationEventService service)
    {
        _service = service;
    }

    /// <inheritdoc/>
    public string Kind => "authorization";

    /// <inheritdoc/>
    public AuthorizationEvent Decode(ReadOnlySpan<byte> body)
        => AuthorizationEventMessageDecoder.Instance.Decode(body);

    /// <inheritdoc/>
    public Task PersistAsync(IReadOnlyList<AuthorizationEvent> events, CancellationToken cancellationToken)
        => _service.CreateAuthorizationEvents(events, cancellationToken);
}

/// <summary>
/// Processor for the <c>eventlog</c> (authentication) queue.
/// </summary>
internal sealed class AuthenticationQueueEventProcessor : IQueueEventProcessor<AuthenticationEvent>
{
    private readonly IAuthenticationEventService _service;

    public AuthenticationQueueEventProcessor(IAuthenticationEventService service)
    {
        _service = service;
    }

    /// <inheritdoc/>
    public string Kind => "authentication";

    /// <inheritdoc/>
    public AuthenticationEvent Decode(ReadOnlySpan<byte> body)
        => AuthenticationEventMessageDecoder.Instance.Decode(body);

    /// <inheritdoc/>
    public Task PersistAsync(IReadOnlyList<AuthenticationEvent> events, CancellationToken cancellationToken)
        => _service.CreateAuthenticationEvents(events, cancellationToken);
}
