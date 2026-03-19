using Altinn.Auth.AuditLog.Core.Enum;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Core.Models;

/// <summary>
/// This model describes an authorization event. An authorization event is an action triggered when a user requests access to an operation
/// </summary>
[ExcludeFromCodeCoverage]
public class AuthorizationEvent
{
    /// <summary>
    /// Session Id of the request
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Date and time of the authorization event. Set by producer of logevents
    /// </summary>
    [Required]
    public DateTimeOffset? Created { get; set; }

    /// <summary>
    /// The userid for the user that requested authorization
    /// </summary>
    public int? SubjectUserId { get; set; }

    /// <summary>
    /// The org code for the org that requested authorization
    /// </summary>
    public string? SubjectOrgCode { get; set; }

    /// <summary>
    /// The org number for the org that requested authorization
    /// </summary>
    public int? SubjectOrgNumber { get; set; }

    /// <summary>
    /// The partyid for the user that requested authorization
    /// </summary>
    public int? SubjectParty { get; set; }

    /// <summary>
    /// The partyId for resource owner when applicable
    /// </summary>
    public int? ResourcePartyId { get; set; }

    /// <summary>
    /// The Main resource Id (app, external resource +)
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Instance Id when applicable
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Type of operation
    /// </summary>
    public required string Operation { get; set; }

    /// <summary>
    /// The Ip adress of the calling party
    /// </summary>
    public string? IpAdress { get; set; }

    /// <summary>
    /// The enriched context request
    /// </summary>
    [JsonConverter(typeof(ContextRequestJsonConverter))]
    public required JsonElement ContextRequestJson { get; set; }

    /// <summary>
    /// Decision for the authorization request
    /// </summary>
    [Required]
    public XacmlContextDecision? Decision { get; set; }

    /// <summary>
    /// The party identifier for the subject
    /// </summary>
    public string? SubjectPartyUuid { get; set; }

    private sealed class ContextRequestJsonConverter
        : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                // if we received a string, we should attempt to deserialize it to an object
                try
                {
                    return ReadJsonString(ref reader);
                }
                catch (JsonException)
                {
                }
            }

            return JsonElement.ParseValue(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            value.WriteTo(writer);
        }

        private static JsonElement ReadJsonString(ref Utf8JsonReader reader)
        {
            var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(checked((int)length));

            try
            {
                var written = reader.CopyString(buffer);
                var subReader = new Utf8JsonReader(buffer.AsSpan(0, written));
                return JsonElement.ParseValue(ref subReader);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
