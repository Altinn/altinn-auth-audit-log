using Altinn.Auth.AuditLog.Core.Models;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Decodes <c>eventlog</c> queue messages into <see cref="AuthenticationEvent"/>. The Authentication component
/// currently publishes version 0 (plain JSON) messages; envelope handling, including future versioned payloads,
/// lives in <see cref="MessageDecoder{TEvent}"/>.
/// </summary>
public sealed class AuthenticationEventMessageDecoder : MessageDecoder<AuthenticationEvent>
{
    /// <summary>
    /// The shared, stateless instance.
    /// </summary>
    public static AuthenticationEventMessageDecoder Instance { get; } = new();

    private AuthenticationEventMessageDecoder()
    {
    }

    /// <inheritdoc/>
    protected override void Validate(AuthenticationEvent @event)
    {
        if (!@event.Created.HasValue)
        {
            MessageDecodeException.ThrowValidationFailed("Authentication event is missing 'created'");
        }
    }
}
