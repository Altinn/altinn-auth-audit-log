using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using Altinn.Auth.AuditLog.Functions.Models;
using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Functions.Clients
{
    /// <summary>
    /// Integration to Auditlog api
    /// </summary>
    public class AuditLogClient : IAuditLogClient
    {
        private readonly ILogger<AuditLogClient> _logger;
        private readonly HttpClient _client;

        public AuditLogClient(
            ILogger<AuditLogClient> logger, 
            HttpClient client,
            IOptions<PlatformSettings> platformSettings)
        {
            _logger = logger;
            _client = client;
            _client.BaseAddress = new Uri(platformSettings.Value.AuditLogApiEndpoint);
        }

        /// <inheritdoc/>
        public async Task SaveAuthenticationEvent(AuthenticationEvent authEvent)
        {
            string endpointUrl = "auditlog/api/v1/authenticationevent";
            var (success, statusCode) = await PostAuthEventToEndpoint(authEvent, endpointUrl);

            if (!success)
            {
                var msg = $"// SaveAuthenticationEvent with id {authEvent.Created} failed with status code {statusCode}";
                _logger.LogError(msg);
                throw new HttpRequestException(msg);
            }
        }

        private async Task<(bool Success, HttpStatusCode StatusCode)> PostAuthEventToEndpoint(AuthenticationEvent authEvent, string endpoint)
        {
            StringContent requestBody = new StringContent(JsonSerializer.Serialize(authEvent), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpoint, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                return (false, response.StatusCode);
            }

            return (true, response.StatusCode);
        }
    }
}
