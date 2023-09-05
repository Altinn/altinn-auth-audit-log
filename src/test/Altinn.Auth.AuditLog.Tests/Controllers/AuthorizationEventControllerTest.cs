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
            Altinn.Auth.AuditLog.Core.Models.Attribute userAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:userid",
                Value = "1",
            };

            Altinn.Auth.AuditLog.Core.Models.Attribute role1Attribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:role",
                Value = "dagl",
            };

            Altinn.Auth.AuditLog.Core.Models.Attribute role2Attribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:role",
                Value = "utinn",
            };

            Altinn.Auth.AuditLog.Core.Models.Attribute actionAttribute = new Core.Models.Attribute()
            {
                Id = "urn:oasis:names:tc:xacml:1.0:action:action-id",
                Value = "read",
                DataType = "http://www.w3.org/2001/XMLSchema#string"
            };

            Altinn.Auth.AuditLog.Core.Models.Attribute instanceAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:instance-id",
                Value = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                IncludeInResult = true
            };
            Altinn.Auth.AuditLog.Core.Models.Attribute orgAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:org",
                Value = "skd"
            };
            Altinn.Auth.AuditLog.Core.Models.Attribute appAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:app",
                Value = "taxreport"
            };
            Altinn.Auth.AuditLog.Core.Models.Attribute partyAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:partyid",
                Value = "1000"
            };
            Altinn.Auth.AuditLog.Core.Models.Attribute taskAttribute = new Core.Models.Attribute()
            {
                Id = "urn:altinn:task",
                Value = "formfilling"
            };

            AccessSubject accessSubject = new AccessSubject();
            accessSubject.Attribute = new List<Core.Models.Attribute>();
            accessSubject.Attribute.Add(userAttribute);
            accessSubject.Attribute.Add(role1Attribute);
            accessSubject.Attribute.Add(role2Attribute);

            Altinn.Auth.AuditLog.Core.Models.Action action = new Altinn.Auth.AuditLog.Core.Models.Action();
            action.Attribute = new List<Core.Models.Attribute>();
            action.Attribute.Add(actionAttribute);

            Altinn.Auth.AuditLog.Core.Models.Resource resource = new Altinn.Auth.AuditLog.Core.Models.Resource();
            resource.Attribute = new List<Core.Models.Attribute>();
            resource.Attribute.Add(instanceAttribute);
            resource.Attribute.Add(orgAttribute);
            resource.Attribute.Add(appAttribute);
            resource.Attribute.Add(partyAttribute);
            resource.Attribute.Add(taskAttribute);


            ContextRequest contextRequest = new ContextRequest();
            contextRequest.AccessSubject = new List<AccessSubject>();
            contextRequest.Action = new List<Altinn.Auth.AuditLog.Core.Models.Action>();
            contextRequest.AccessSubject = new List<AccessSubject>();
            contextRequest.Resources = new List<Resource>();
            contextRequest.AccessSubject.Add(accessSubject);
            contextRequest.Action.Add(action);
            contextRequest.Resources.Add(resource);

            AuthorizationEvent authorizationEvent = new AuthorizationEvent()
            {
                SubjectUserId = "2000000",
                SubjectParty = "",
                ResourcePartyId = "1000",
                Resource = "taxreport",
                InstanceId = "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713",
                Operation = "read",
                TimeToDelete = "",
                IpAdress = "192.0.2.1",
                ContextRequestJson = contextRequest

            };

            return authorizationEvent;
        }
    }
}
