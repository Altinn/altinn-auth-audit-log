using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Functions.Clients
{
    /// <summary>
    /// Integration to Auditlog api
    /// </summary>
    public class AuditLogClient : IAuditLogClient
    {
        private readonly ILogger<IAuditLogClient> _logger;
        private readonly HttpClient _client;

        public AuditLogClient(
            ILogger<IAuditLogClient> logger, 
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
            var (success, statusCode) = await PostAuthEventToEndpoint(authEvent, null, endpointUrl);

            if (!success)
            {
                var msg = $"// SaveAuthenticationEvent failed with status code {statusCode}";
                _logger.LogError("SaveAuthenticationEvent failed with status code {statusCode}", statusCode);
                throw new HttpRequestException(msg);
            }
        }

        /// <inheritdoc/>
        public async Task SaveAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            string endpointUrl = "auditlog/api/v1/authorizationevent";
            var (success, statusCode) = await PostAuthEventToEndpoint(null, authorizationEvent, endpointUrl);

            if (!success)
            {
                string msg = $"SaveAuthorizationEvent failed with status code {statusCode}";
                _logger.LogError("SaveAuthorizationEvent failed with status code {statusCode}", statusCode);
                throw new HttpRequestException(msg);
            }
        }

        private async Task<(bool Success, HttpStatusCode StatusCode)> PostAuthEventToEndpoint(AuthenticationEvent? authEvent, AuthorizationEvent? authorizationEvent, string endpoint)
        {
            StringContent? requestBody = null;
            if (authEvent != null)
            {
                requestBody = new StringContent(JsonSerializer.Serialize(authEvent), Encoding.UTF8, "application/json");
            }
            else if(authorizationEvent != null)
            {
                requestBody = new StringContent(JsonSerializer.Serialize(authorizationEvent), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = await _client.PostAsync(endpoint, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                return (false, response.StatusCode);
            }

            return (true, response.StatusCode);
        }
    }
}
