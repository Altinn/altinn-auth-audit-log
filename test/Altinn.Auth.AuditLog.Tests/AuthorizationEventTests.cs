using Altinn.Auth.AuditLog.Core.Models;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests;

public class AuthorizationEventTests
{
    [Fact]
    public void Deserialize_Normalizes_ContextRequestJson()
    {
        var authorizationEvent = new AuthorizationEvent()
        {
            SubjectUserId = 2000000,
            Created = new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero),
            ResourcePartyId = 1000,
            Resource = "taxreport",
            InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
            Operation = "read",
            IpAdress = "192.0.2.1",
            ContextRequestJson = JsonSerializer.Deserialize<JsonElement>("""{"ReturnPolicyIdList":false,"CombinedDecision":false,"XPathVersion":null,"Attributes":[{"Id":null,"Content":null,"Attributes":[{"Issuer":null,"AttributeId":"urn:altinn:org","IncludeInResult":false,"AttributeValues":[{"Value":"skd","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[{"IsNamespaceDeclaration":false,"Name":{"LocalName":"DataType","Namespace":{"NamespaceName":""},"NamespaceName":""},"NextAttribute":null,"NodeType":2,"PreviousAttribute":null,"Value":"http://www.w3.org/2001/XMLSchema#string","BaseUri":"","Document":null,"Parent":null}],"Elements":[]}]}],"Category":"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject"},{"Id":null,"Content":null,"Attributes":[{"Issuer":null,"AttributeId":"urn:altinn:instance-id","IncludeInResult":false,"AttributeValues":[{"Value":"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[{"IsNamespaceDeclaration":false,"Name":{"LocalName":"DataType","Namespace":{"NamespaceName":""},"NamespaceName":""},"NextAttribute":null,"NodeType":2,"PreviousAttribute":null,"Value":"http://www.w3.org/2001/XMLSchema#string","BaseUri":"","Document":null,"Parent":null}],"Elements":[]}]},{"Issuer":null,"AttributeId":"urn:altinn:org","IncludeInResult":false,"AttributeValues":[{"Value":"skd","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]},{"Issuer":null,"AttributeId":"urn:altinn:app","IncludeInResult":false,"AttributeValues":[{"Value":"taxreport","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]},{"Issuer":null,"AttributeId":"urn:altinn:task","IncludeInResult":false,"AttributeValues":[{"Value":"Task_1","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]},{"Issuer":null,"AttributeId":"urn:altinn:partyid","IncludeInResult":true,"AttributeValues":[{"Value":"1000","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[],"Elements":[]}]}],"Category":"urn:oasis:names:tc:xacml:3.0:attribute-category:resource"},{"Id":null,"Content":null,"Attributes":[{"Issuer":null,"AttributeId":"urn:oasis:names:tc:xacml:1.0:action:action-id","IncludeInResult":false,"AttributeValues":[{"Value":"read","DataType":"http://www.w3.org/2001/XMLSchema#string","Attributes":[{"IsNamespaceDeclaration":false,"Name":{"LocalName":"DataType","Namespace":{"NamespaceName":""},"NamespaceName":""},"NextAttribute":null,"NodeType":2,"PreviousAttribute":null,"Value":"http://www.w3.org/2001/XMLSchema#string","BaseUri":"","Document":null,"Parent":null}],"Elements":[]}]}],"Category":"urn:oasis:names:tc:xacml:3.0:attribute-category:action"},{"Id":null,"Content":null,"Attributes":[],"Category":"urn:oasis:names:tc:xacml:3.0:attribute-category:environment"}],"RequestReferences":[]}"""),
            Decision = Core.Enum.XacmlContextDecision.Permit,
            SubjectPartyUuid = "732f9355-c0e4-4df8-98f0-8e773809ff63"
        };

        var raw = authorizationEvent.ContextRequestJson;
        authorizationEvent.ContextRequestJson = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(raw.ToString()));
        Assert.Equal(JsonValueKind.String, authorizationEvent.ContextRequestJson.ValueKind);

        var serialized = JsonSerializer.Serialize(authorizationEvent, JsonSerializerOptions.Web);
        var deserialized = JsonSerializer.Deserialize<AuthorizationEvent>(serialized, JsonSerializerOptions.Web);

        Assert.Equal(raw, deserialized!.ContextRequestJson, JsonElement.DeepEquals);
    }
}
