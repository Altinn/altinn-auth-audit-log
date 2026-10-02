using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Shared envelope handling for audit-log queue messages. The input is the message body <em>after</em> the
/// queue-level base64 decoding (what the Azure Functions <c>QueueTrigger</c> handed to the processors).
/// </summary>
/// <remarks>
/// <para>Supported envelopes, in order of detection:</para>
/// <list type="number">
///   <item><b>Version 0 (legacy):</b> raw JSON starting with <c>{</c>.</item>
///   <item><b>Versioned:</b> a two digit ASCII version prefix followed by the payload. Version <c>01</c> is Brotli compressed JSON.</item>
///   <item><b>Base64 wrapped:</b> a base64 string that, once decoded, is one of the above (PDP double-base64-encodes).</item>
/// </list>
/// <para>
/// All failures are reported as <see cref="MessageDecodeException"/>, which the consumer treats as permanent (poison).
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The event type.</typeparam>
public abstract class MessageDecoder<TEvent>
    where TEvent : class
{
    private const int MinimumDecompressBuffer = 4 * 1024;

    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    /// <summary>
    /// Decodes and validates a message body.
    /// </summary>
    /// <param name="message">The message body (after queue-level decoding).</param>
    /// <returns>The decoded event.</returns>
    /// <exception cref="MessageDecodeException">If the message cannot be decoded or fails validation.</exception>
    public TEvent Decode(ReadOnlySpan<byte> message)
    {
        message = TrimStart(message);
        if (message.Length < 2)
        {
            MessageDecodeException.ThrowTooSmall(message.Length);
        }

        if (message[0] == (byte)'{')
        {
            return Finish(ParseJson(message));
        }

        if (TryParseVersion(message, out var version))
        {
            return Finish(DecodeVersioned(version, message[2..]));
        }

        // Not raw JSON and no version prefix: must be base64 wrapped.
        var buffer = ArrayPool<byte>.Shared.Rent(message.Length);
        try
        {
            var outcome = Base64.DecodeFromUtf8(message, buffer, out var bytesConsumed, out var bytesWritten);
            if (outcome != OperationStatus.Done || bytesConsumed != message.Length)
            {
                MessageDecodeException.ThrowInvalidBase64();
            }

            var decoded = TrimStart(buffer.AsSpan(0, bytesWritten));
            if (TryParseVersion(decoded, out version))
            {
                return Finish(DecodeVersioned(version, decoded[2..]));
            }

            return Finish(ParseJson(decoded));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Validates a decoded event; throw via <see cref="MessageDecodeException.ThrowValidationFailed"/> on failure.
    /// </summary>
    protected abstract void Validate(TEvent @event);

    private TEvent Finish(TEvent @event)
    {
        Validate(@event);
        return @event;
    }

    private TEvent DecodeVersioned(ushort version, ReadOnlySpan<byte> payload)
    {
        switch (version)
        {
            case 0:
                return ParseJson(payload);

            case 1:
                return DecodeBrotli(payload);

            default:
                MessageDecodeException.ThrowUnsupportedVersion(version);
                return null!;
        }
    }

    // Version 01: Brotli compressed JSON, decompressed span-to-span into a pooled buffer.
    private TEvent DecodeBrotli(ReadOnlySpan<byte> payload)
    {
        using var decoder = new BrotliDecoder();
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(MinimumDecompressBuffer, payload.Length * 4));

        try
        {
            var written = 0;
            while (true)
            {
                var status = decoder.Decompress(payload, buffer.AsSpan(written), out var consumed, out var produced);
                payload = payload[consumed..];
                written += produced;

                switch (status)
                {
                    case OperationStatus.Done:
                        return ParseJson(buffer.AsSpan(0, written));

                    case OperationStatus.DestinationTooSmall:
                        var larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        buffer.AsSpan(0, written).CopyTo(larger);
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                        buffer = larger;
                        break;

                    default:
                        // NeedMoreData (truncated) or InvalidData
                        MessageDecodeException.ThrowInvalidCompression();
                        return null!;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static TEvent ParseJson(ReadOnlySpan<byte> json)
    {
        TEvent? result;
        try
        {
            var reader = new Utf8JsonReader(json);
            result = JsonSerializer.Deserialize<TEvent>(ref reader, _jsonOptions);
        }
        catch (JsonException ex)
        {
            MessageDecodeException.ThrowInvalidJson(ex);
            return null!;
        }

        if (result is null)
        {
            MessageDecodeException.ThrowInvalidJson();
        }

        return result;
    }

    private static bool TryParseVersion(ReadOnlySpan<byte> message, out ushort version)
    {
        if (message.Length >= 2)
        {
            return ushort.TryParse(message[..2], NumberStyles.None, provider: null, out version);
        }

        version = default;
        return false;
    }

    private static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> span)
    {
        var i = 0;
        while (i < span.Length && span[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        return span[i..];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
