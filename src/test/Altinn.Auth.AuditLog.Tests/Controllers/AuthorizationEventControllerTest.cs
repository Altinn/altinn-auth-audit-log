using Altinn.Auth.AuditLog.Controllers;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Tests.Utils;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Tests.Controllers
{
    /// <summary>
    /// Test class for <see cref="AuthenticationEventController"></see>
    /// </summary>
    [Collection("AuthorizationEvent Tests")]
    public class AuthorizationEventControllerTest : IClassFixture<CustomWebApplicationFactory<AuthorizationEventController>>
    {
        private readonly CustomWebApplicationFactory<AuthorizationEventController> _factory;
        private HttpClient _client;
        private readonly Mock<IAuthorizationEventRepository> _authorizationEventRepositoryMock;

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Constructor setting up factory, test client and dependencies
        /// </summary>
        /// <param name="factory">CustomWebApplicationFactory</param>
        public AuthorizationEventControllerTest(CustomWebApplicationFactory<AuthorizationEventController> factory)
        {
            _factory = factory;
            _client = SetupUtil.GetTestClient(factory);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _authorizationEventRepositoryMock = new Mock<IAuthorizationEventRepository>();
        }

        [Fact]
        public async Task CreateAuthorizationEvent_Ok()
        {


            string requestUri = "auditlog/api/v1/authorizationevent/";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(GetAuthorizationEvent()), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await _client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateAuthorizationEvent_Badrequest_nullobject()
        {
            AuthorizationEvent authorizationEvent = null;

            string requestUri = "auditlog/api/v1/authorizationevent/";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(authorizationEvent), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await _client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private AuthorizationEvent GetAuthorizationEvent()
        {
            AuthorizationEvent authorizationEvent = new AuthorizationEvent()
            {
                SubjectUserId = 2000000,
                ResourcePartyId = 1000,
                Resource = "taxreport",
                InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                Operation = "read",
                IpAdress = "192.0.2.1",
                ContextRequestJson = "{\"ReturnPolicyIdList\":false,\"CombinedDecision\":false,\"XPathVersion\":null,\"Attributes\":[{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"skd\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:1.0:subject-category:access-subject\"},{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:altinn:instance-id\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:org\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"skd\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:app\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"taxreport\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:task\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"Task_1\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]},{\"Issuer\":null,\"AttributeId\":\"urn:altinn:partyid\",\"IncludeInResult\":true,\"AttributeValues\":[{\"Value\":\"1000\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:resource\"},{\"Id\":null,\"Content\":null,\"Attributes\":[{\"Issuer\":null,\"AttributeId\":\"urn:oasis:names:tc:xacml:1.0:action:action-id\",\"IncludeInResult\":false,\"AttributeValues\":[{\"Value\":\"read\",\"DataType\":\"http://www.w3.org/2001/XMLSchema#string\",\"Attributes\":[{\"IsNamespaceDeclaration\":false,\"Name\":{\"LocalName\":\"DataType\",\"Namespace\":{\"NamespaceName\":\"\"},\"NamespaceName\":\"\"},\"NextAttribute\":null,\"NodeType\":2,\"PreviousAttribute\":null,\"Value\":\"http://www.w3.org/2001/XMLSchema#string\",\"BaseUri\":\"\",\"Document\":null,\"Parent\":null}],\"Elements\":[]}]}],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:action\"},{\"Id\":null,\"Content\":null,\"Attributes\":[],\"Category\":\"urn:oasis:names:tc:xacml:3.0:attribute-category:environment\"}],\"RequestReferences\":[]}"

            };

            return authorizationEvent;
        }
    }
}
