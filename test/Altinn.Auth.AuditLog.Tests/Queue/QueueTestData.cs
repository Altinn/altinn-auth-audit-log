using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests.Queue;

/// <summary>
/// Produces authorization/authentication events in every wire format the queues have carried.
/// Ported from <c>Altinn.Auth.AuditLog.Functions.Tests/Helpers/TestDataHelper.cs</c>.
/// </summary>
public static class QueueTestData
{
    public static AuthorizationEvent GetAuthorizationEvent()
    {
        return new AuthorizationEvent()
        {
            SubjectUserId = 2000000,
            Created = new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero),
            ResourcePartyId = 1000,
            Resource = "taxreport",
            InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
            Operation = "read",
            IpAdress = "192.0.2.1",
            ContextRequestJson = JsonSerializer.Deserialize<JsonElement>("""{"ReturnPolicyIdList":false,"CombinedDecision":false,"XPathVersion":null,"Attributes":[{"Id":null,"Content":null,"Attributes":[{"Issuer":null,"AttributeId":"urn:altinn:org","IncludeInResult":false,"AttributeValues":[{"Value":"skd","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]}],"Category":"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject"},{"Id":null,"Content":null,"Attributes":[{"Issuer":null,"AttributeId":"urn:oasis:names:tc:xacml:1.0:action:action-id","IncludeInResult":false,"AttributeValues":[{"Value":"read","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]}],"Category":"urn:oasis:names:tc:xacml:3.0:attribute-category:action"}],"RequestReferences":[]}"""),
            Decision = XacmlContextDecision.Permit,
            SubjectPartyUuid = "732f9355-c0e4-4df8-98f0-8e773809ff63",
            TraceId = "0af7651916cd43dd8448eb211c80319c",
        };
    }

    public static byte[] GetAuthorizationEvent_JsonData(AuthorizationEvent? authorizationEvent = null)
        => JsonSerializer.SerializeToUtf8Bytes(authorizationEvent ?? GetAuthorizationEvent(), JsonSerializerOptions.Web);

    /// <summary>Legacy: base64 of the JSON (what the Functions host saw before the second base64 layer was removed).</summary>
    public static byte[] GetAuthorizationEvent_LegacyFormat()
        => Encoding.UTF8.GetBytes(Convert.ToBase64String(GetAuthorizationEvent_JsonData()));

    /// <summary>Legacy: raw JSON.</summary>
    public static byte[] GetAuthorizationEvent_LegacyFormat_NonBase64()
        => GetAuthorizationEvent_JsonData();

    /// <summary>Version 01: "01" + Brotli(JSON).</summary>
    public static byte[] GetAuthorizationEvent_V1Format(AuthorizationEvent? authorizationEvent = null)
    {
        using var stream = new MemoryStream();
        stream.Write("01"u8);

        using (var compressor = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            JsonSerializer.Serialize(compressor, authorizationEvent ?? GetAuthorizationEvent(), JsonSerializerOptions.Web);
        }

        return stream.ToArray();
    }

    /// <summary>Version 01 wrapped in base64 — what PDP's double base64 encoding looks like after queue-level decoding.</summary>
    public static byte[] GetAuthorizationEvent_V1Format_Base64Wrapped(AuthorizationEvent? authorizationEvent = null)
        => Encoding.UTF8.GetBytes(Convert.ToBase64String(GetAuthorizationEvent_V1Format(authorizationEvent)));

    public static AuthenticationEvent GetAuthenticationEvent()
    {
        return new AuthenticationEvent()
        {
            UserId = 20000003,
            Created = DateTimeOffset.Parse("2023-09-07T06:24:43.971899Z"),
            AuthenticationMethod = AuthenticationMethod.BankID,
            EventType = AuthenticationEventType.Authenticate,
            AuthenticationLevel = SecurityLevel.VerySensitive,
            IpAddress = "192.0.2.7",
            IsAuthenticated = true,
            SessionId = "session-1",
        };
    }

    /// <summary>The exact document shape the Authentication component produces (PascalCase, enum names).</summary>
    public static byte[] GetAuthenticationEvent_JsonData()
        => Encoding.UTF8.GetBytes("{\"Created\":\"2023-09-07T06:24:43.971899Z\",\"UserId\":20000003,\"EventType\":\"Authenticate\",\"AuthenticationMethod\":\"BankId\",\"AuthenticationLevel\":\"VerySensitive\",\"IpAddress\":\"192.0.2.7\",\"IsAuthenticated\":true,\"SessionId\":\"session-1\"}\r\n");

    public static byte[] GetAuthenticationEvent_Base64Wrapped()
        => Encoding.UTF8.GetBytes(Convert.ToBase64String(GetAuthenticationEvent_JsonData()));
}
