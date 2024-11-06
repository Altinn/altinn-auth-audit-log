using Altinn.Auth.AuditLog.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Tests.Health
{
    /// <summary>
    /// Health check 
    /// </summary>
    public class HealthCheckTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {

        /// <summary>
        /// Verify that component responds on health check
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task VerifyHealthCheck_OK()
        {
            using var client = CreateEventClient();

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/health");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
            string content = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private HttpClient CreateEventClient()
        {
            var client = CreateClient();
            return client;
        }
    }
}
