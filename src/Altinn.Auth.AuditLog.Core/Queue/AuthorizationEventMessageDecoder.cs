using Altinn.Auth.AuditLog.Core.Models;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Decodes <c>authorizationeventlog</c> queue messages into <see cref="AuthorizationEvent"/>.
/// Envelope handling (legacy JSON, versioned/Brotli, base64 wrapped) lives in <see cref="MessageDecoder{TEvent}"/>;
/// this class applies the same validation rules as the HTTP endpoint ([Required]) and the repository guards.
/// </summary>
public sealed class AuthorizationEventMessageDecoder : MessageDecoder<AuthorizationEvent>
{
    /// <summary>
    /// The shared, stateless instance.
    /// </summary>
    public static AuthorizationEventMessageDecoder Instance { get; } = new();

    private AuthorizationEventMessageDecoder()
    {
    }

    /// <inheritdoc/>
    protected override void Validate(AuthorizationEvent evt)
    {
        if (!evt.Created.HasValue)
        {
            MessageDecodeException.ThrowValidationFailed("Authorization event is missing 'created'");
        }

        if (!evt.Decision.HasValue)
        {
            MessageDecodeException.ThrowValidationFailed("Authorization event is missing 'decision'");
        }

        if (string.IsNullOrEmpty(evt.Operation))
        {
            MessageDecodeException.ThrowValidationFailed("Authorization event is missing 'operation'");
        }

        if (evt.ContextRequestJson.ValueKind != JsonValueKind.Object)
        {
            MessageDecodeException.ThrowValidationFailed("Authorization event 'contextRequestJson' must be an object");
        }
    }
}
