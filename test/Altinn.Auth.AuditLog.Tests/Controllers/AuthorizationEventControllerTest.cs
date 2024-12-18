using Altinn.Auth.AuditLog.Controllers;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests.Controllers
{
    /// <summary>
    /// Test class for <see cref="AuthenticationEventController"></see>
    /// </summary>
    [Collection("AuthorizationEvent Tests")]
    public class AuthorizationEventControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private HttpClient CreateEventClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        [Fact]
        public async Task CreateAuthorizationEvent_Ok()
        {
            using var client = CreateEventClient();
            string requestUri = "auditlog/api/v1/authorizationevent/";
            Mock<IAuthorizationEventRepository> authzEventRepository = new Mock<IAuthorizationEventRepository>();
            authzEventRepository.Setup(q => q.InsertAuthorizationEvent(It.IsAny<AuthorizationEvent>())).Returns(Task.FromResult(GetAuthorizationEvent(true)));

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(GetAuthorizationEvent(true)), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateAuthorizationEvent_Badrequest_nullobject()
        {
            AuthorizationEvent authorizationEvent = null;
            using var client = CreateEventClient();
            string requestUri = "auditlog/api/v1/authorizationevent/";
            Mock<IAuthorizationEventRepository> authzEventRepository = new Mock<IAuthorizationEventRepository>();
            authzEventRepository.Setup(q => q.InsertAuthorizationEvent(It.IsAny<AuthorizationEvent>())).Returns(Task.FromResult(authorizationEvent));

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(authorizationEvent), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateAuthorizationEvent_Badrequest_created()
        {
            using var client = CreateEventClient();
            string requestUri = "auditlog/api/v1/authorizationevent/";
            Mock<IAuthorizationEventRepository> authzEventRepository = new Mock<IAuthorizationEventRepository>();
            authzEventRepository.Setup(q => q.InsertAuthorizationEvent(It.IsAny<AuthorizationEvent>())).Throws<System.ArgumentException>();

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(GetAuthorizationEvent(false)), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private AuthorizationEvent GetAuthorizationEvent(bool isCreated)
        {
            AuthorizationEvent authorizationEvent = new AuthorizationEvent()
            {
                Created = isCreated ? TimeProvider.GetUtcNow() : null,
                SubjectUserId = 2000000,
                ResourcePartyId = 1000,
                Resource = "taxreport",
                InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                Operation = "read",
                IpAdress = "192.0.2.1",
                ContextRequestJson = "{\"ReturnPolicyIdList\":false,\"CombinedDecision\":false,\"XPathVersion\":null,\"Attributes\":[{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"skd\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject\"},{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:instance-id\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"skd\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:app\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"taxreport\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:task\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"Task_1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:partyid\",\"IncludeInResult\":true,\"AttributeValues\":[{\"Value\":\"1000\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:resource\"},{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:oasis:names:tc:xacml:1.0:action:action-id\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"read\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:action\"},{\"Id\":null,\"Content\":null,\"Attributes\":[],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:environment\"}],\"RequestReferences\":[]}",
                Decision = Core.Enum.XacmlContextDecision.Permit,
                UserIdentifier = "732f9355-c0e4-4df8-98f0-8e773809ff63"
            };

            return authorizationEvent;
        }
    }
}
