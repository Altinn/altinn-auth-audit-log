using Altinn.Auth.AuditLog.Core.Models;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Decodes raw <c>authorizationeventlog</c> queue messages into <see cref="AuthorizationEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// The input is the message body <em>after</em> the queue-level base64 decoding (the same representation
/// the Azure Functions <c>QueueTrigger</c> handed to <c>AuthorizationEventsProcessor</c>). The following
/// wire formats are supported, in order of detection:
/// </para>
/// <list type="number">
///   <item>Legacy: raw JSON starting with <c>{</c>.</item>
///   <item>Versioned: a two digit ASCII version prefix followed by the payload. Version <c>01</c> is Brotli compressed JSON.</item>
///   <item>Base64 wrapped: a base64 string that, once decoded, is either versioned (PDP double-base64-encodes) or legacy JSON.</item>
/// </list>
/// <para>
/// All failures are reported as <see cref="MessageDecodeException"/>, which the consumer treats as permanent (poison).
/// </para>
/// </remarks>
public static class AuthorizationEventMessageDecoder
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    /// <summary>
    /// Decodes and validates a message body.
    /// </summary>
    /// <param name="message">The message body.</param>
    /// <returns>The decoded event.</returns>
    /// <exception cref="MessageDecodeException">If the message cannot be decoded or fails validation.</exception>
    public static AuthorizationEvent Decode(ReadOnlyMemory<byte> message)
    {
        if (message.Length < 2)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.TooSmall, $"Authorization event message is too small ({message.Length} bytes)");
        }

        var span = message.Span;
        if (span[0] == (byte)'{')
        {
            return Validate(ParseJson(message));
        }

        if (TryParseVersion(span[..2], out var version))
        {
            return Validate(DecodeVersioned(version, message[2..]));
        }

        // Not raw JSON and no version prefix: must be base64 wrapped.
        var buffer = ArrayPool<byte>.Shared.Rent(message.Length);
        try
        {
            var outcome = Base64.DecodeFromUtf8(span, buffer, out var bytesConsumed, out var bytesWritten);
            if (outcome != OperationStatus.Done || bytesConsumed != message.Length)
            {
                throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidBase64, "Authorization event message is neither JSON, versioned, nor valid base64");
            }

            var decoded = buffer.AsMemory(0, bytesWritten);
            if (decoded.Length >= 2 && TryParseVersion(decoded.Span[..2], out version))
            {
                return Validate(DecodeVersioned(version, decoded[2..]));
            }

            return Validate(ParseJson(decoded));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool TryParseVersion(ReadOnlySpan<byte> firstBytes, out ushort version)
        => ushort.TryParse(firstBytes, NumberStyles.None, provider: null, out version);

    private static AuthorizationEvent DecodeVersioned(ushort version, ReadOnlyMemory<byte> payload)
    {
        return version switch
        {
            01 => DecodeV01(payload),
            _ => throw new MessageDecodeException(MessageDecodeException.Reasons.UnsupportedVersion, $"Unsupported authorization event version: {version}"),
        };
    }

    // Version 01: Brotli compressed JSON.
    private static AuthorizationEvent DecodeV01(ReadOnlyMemory<byte> payload)
    {
        using var jsonStream = new MemoryStream();

        try
        {
            using var receivedStream = new ReadOnlyMemoryStream(payload);
            using var decodedStream = new BrotliStream(receivedStream, CompressionMode.Decompress);
            decodedStream.CopyTo(jsonStream);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidCompression, "Failed to Brotli-decompress authorization event", ex);
        }

        if (!jsonStream.TryGetBuffer(out var segment))
        {
            segment = new ArraySegment<byte>(jsonStream.ToArray());
        }

        return ParseJson(segment.AsMemory());
    }

    private static AuthorizationEvent ParseJson(ReadOnlyMemory<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json.Span);
            var result = JsonSerializer.Deserialize<AuthorizationEvent>(ref reader, _jsonOptions);
            return result ?? throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidJson, "Authorization event JSON was null");
        }
        catch (JsonException ex)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidJson, "Authorization event is not valid JSON", ex);
        }
    }

    /// <summary>
    /// Applies the same rules as the HTTP endpoint ([Required] attributes) and the repository guards.
    /// </summary>
    private static AuthorizationEvent Validate(AuthorizationEvent authorizationEvent)
    {
        if (!authorizationEvent.Created.HasValue)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.ValidationFailed, "Authorization event is missing 'created'");
        }

        if (!authorizationEvent.Decision.HasValue)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.ValidationFailed, "Authorization event is missing 'decision'");
        }

        if (string.IsNullOrEmpty(authorizationEvent.Operation))
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.ValidationFailed, "Authorization event is missing 'operation'");
        }

        if (authorizationEvent.ContextRequestJson.ValueKind != JsonValueKind.Object)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.ValidationFailed, "Authorization event 'contextRequestJson' must be an object");
        }

        return authorizationEvent;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    /// Minimal read-only stream over a <see cref="ReadOnlyMemory{T}"/> to avoid copying the payload before decompression.
    /// </summary>
    private sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => memory.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer)
        {
            var remaining = memory.Span[_position..];
            var count = Math.Min(remaining.Length, buffer.Length);
            remaining[..count].CopyTo(buffer);
            _position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
