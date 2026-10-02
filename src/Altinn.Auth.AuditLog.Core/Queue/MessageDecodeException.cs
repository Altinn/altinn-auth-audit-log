using System.Diagnostics.CodeAnalysis;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Thrown when a queue message cannot be decoded into an audit event. This is a permanent failure:
/// retrying the same message will never succeed, so the consumer should move it to the poison queue.
/// </summary>
public sealed class MessageDecodeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageDecodeException"/> class.
    /// </summary>
    /// <param name="reason">A short, stable reason code (used as a metric tag), see <see cref="Reasons"/>.</param>
    /// <param name="message">A human readable message.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public MessageDecodeException(string reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets a short, stable reason code for the failure (for instance <c>invalid_json</c>).
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Throws for a message that is too short to carry anything.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowTooSmall(int length)
        => throw new MessageDecodeException(Reasons.TooSmall, $"Message is too small ({length} bytes)");

    /// <summary>
    /// Throws for a message that is neither JSON, versioned, nor valid base64.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidBase64()
        => throw new MessageDecodeException(Reasons.InvalidBase64, "Message is neither JSON, versioned, nor valid base64");

    /// <summary>
    /// Throws for a version prefix this decoder does not understand.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowUnsupportedVersion(ushort version)
        => throw new MessageDecodeException(Reasons.UnsupportedVersion, $"Unsupported message version: {version}");

    /// <summary>
    /// Throws for a payload that fails to decompress.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidCompression(Exception? innerException = null)
        => throw new MessageDecodeException(Reasons.InvalidCompression, "Failed to decompress message payload", innerException);

    /// <summary>
    /// Throws for a payload that is not valid JSON for the expected event type.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidJson(Exception? innerException = null)
        => throw new MessageDecodeException(Reasons.InvalidJson, "Message payload is not valid JSON for the expected event", innerException);

    /// <summary>
    /// Throws for an event that decoded but fails validation.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowValidationFailed(string message)
        => throw new MessageDecodeException(Reasons.ValidationFailed, message);

    /// <summary>
    /// Reason codes used by the decoders.
    /// </summary>
    public static class Reasons
    {
        public const string TooSmall = "too_small";
        public const string InvalidBase64 = "invalid_base64";
        public const string UnsupportedVersion = "unsupported_version";
        public const string InvalidCompression = "invalid_compression";
        public const string InvalidJson = "invalid_json";
        public const string ValidationFailed = "validation_failed";
    }
}
