using Altinn.Auth.AuditLog.Controllers;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories;
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
    [Collection("AuthenticationEvent Tests")]
    public class AuthenticationEventControllerTest : IClassFixture<CustomWebApplicationFactory<AuthenticationEventController>>
    {
        private readonly CustomWebApplicationFactory<AuthenticationEventController> _factory;
        private HttpClient _client;
        private readonly Mock<IAuthenticationEventRepository> _authenticationEventRepositoryMock;

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Constructor setting up factory, test client and dependencies
        /// </summary>
        /// <param name="factory">CustomWebApplicationFactory</param>
        public AuthenticationEventControllerTest(CustomWebApplicationFactory<AuthenticationEventController> factory)
        {
            _factory = factory;
            _client = SetupUtil.GetTestClient(factory);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _authenticationEventRepositoryMock = new Mock<IAuthenticationEventRepository>();
        }

        [Fact]
        public async Task CreateAuthenticationEvent_Ok()
        {
            AuthenticationEvent authenticationEvent = new AuthenticationEvent()
            {
                UserId = "20000003",
                TimeStamp = DateTime.UtcNow,
                AuthenticationMethod = "BankId",
                EventType = "LoggedIn",
                SessionId = "83343b4c-865d-4e6c-888d-33bc7533ea2d",
                AuthenticationLevel = "4",

            };

            string requestUri = "auditlog/api/v1/authenticationevent/";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(authenticationEvent), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await _client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
