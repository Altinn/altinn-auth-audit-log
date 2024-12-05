using Altinn.Auth.AuditLog.Controllers;
using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests.Controllers
{
    /// <summary>
    /// Test class for <see cref="AuthenticationEventController"></see>
    /// </summary>
    [Collection("AuthenticationEvent Tests")]
    public class AuthenticationEventControllerTest(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private HttpClient CreateEventClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        [Fact(Skip = "Ignored")]
        public async Task CreateAuthenticationEvent_Ok()
        {
            using var client = CreateEventClient();
            AuthenticationEvent authenticationEvent = new AuthenticationEvent()
            {
                Created = TimeProvider.GetUtcNow(),
                UserId = 20000003,
                AuthenticationMethod = AuthenticationMethod.BankID,
                EventType = AuthenticationEventType.Authenticate,
                AuthenticationLevel = SecurityLevel.VerySensitive
            };

            string requestUri = "auditlog/api/v1/authenticationevent/";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(authenticationEvent), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateAuthenticationEvent_Badrequest_nullobject()
        {
            AuthenticationEvent authenticationEvent = null;
            using var client = CreateEventClient();
            string requestUri = "auditlog/api/v1/authenticationevent/";

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(authenticationEvent), Encoding.UTF8, "application/json")
            };

            httpRequestMessage.Headers.Add("Accept", "application/json");
            httpRequestMessage.Headers.Add("ContentType", "application/json");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
