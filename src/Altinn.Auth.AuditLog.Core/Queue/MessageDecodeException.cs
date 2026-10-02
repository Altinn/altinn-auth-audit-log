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
    /// <param name="reason">A short, stable reason code (used as a metric tag).</param>
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
