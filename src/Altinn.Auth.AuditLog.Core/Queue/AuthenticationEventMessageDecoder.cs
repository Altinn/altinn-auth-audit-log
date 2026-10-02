using Altinn.Auth.AuditLog.Core.Models;
using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Core.Queue;

/// <summary>
/// Decodes raw <c>eventlog</c> queue messages into <see cref="AuthenticationEvent"/>.
/// </summary>
/// <remarks>
/// The input is the message body <em>after</em> the queue-level base64 decoding, i.e. the JSON document the
/// Authentication component produced. For robustness a message that is still base64 encoded is decoded once more.
/// All failures are reported as <see cref="MessageDecodeException"/>, which the consumer treats as permanent (poison).
/// </remarks>
public static class AuthenticationEventMessageDecoder
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    /// <summary>
    /// Decodes and validates a message body.
    /// </summary>
    /// <param name="message">The message body.</param>
    /// <returns>The decoded event.</returns>
    /// <exception cref="MessageDecodeException">If the message cannot be decoded or fails validation.</exception>
    public static AuthenticationEvent Decode(ReadOnlyMemory<byte> message)
    {
        if (message.Length < 2)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.TooSmall, $"Authentication event message is too small ({message.Length} bytes)");
        }

        if (StartsWithObject(message.Span))
        {
            return Validate(ParseJson(message));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(message.Length);
        try
        {
            var outcome = Base64.DecodeFromUtf8(message.Span, buffer, out var bytesConsumed, out var bytesWritten);
            if (outcome != OperationStatus.Done || bytesConsumed != message.Length)
            {
                throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidBase64, "Authentication event message is neither JSON nor valid base64");
            }

            return Validate(ParseJson(buffer.AsMemory(0, bytesWritten)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool StartsWithObject(ReadOnlySpan<byte> span)
    {
        foreach (var b in span)
        {
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return b == (byte)'{';
        }

        return false;
    }

    private static AuthenticationEvent ParseJson(ReadOnlyMemory<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json.Span);
            var result = JsonSerializer.Deserialize<AuthenticationEvent>(ref reader, _jsonOptions);
            return result ?? throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidJson, "Authentication event JSON was null");
        }
        catch (JsonException ex)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidJson, "Authentication event is not valid JSON", ex);
        }
    }

    private static AuthenticationEvent Validate(AuthenticationEvent authenticationEvent)
    {
        if (!authenticationEvent.Created.HasValue)
        {
            throw new MessageDecodeException(MessageDecodeException.Reasons.ValidationFailed, "Authentication event is missing 'created'");
        }

        return authenticationEvent;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
