using System;
using System.Text.Json;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Models;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Altinn.Auth.AuditLog.Functions
{
    public class AuthorizationEventsProcessor
    {
        private readonly ILogger<AuthorizationEventsProcessor> _logger;
        private readonly IAuditLogClient _auditLogClient;

        public AuthorizationEventsProcessor(
            ILogger<AuthorizationEventsProcessor> logger,
            IAuditLogClient auditLogClient)
        {
            _logger = logger;
            _auditLogClient = auditLogClient;
        }

        /// <summary>
        /// Reads authorization event from authorization eventlog queue and  post it to auditlog api
        /// </summary>
        [Function(nameof(AuthorizationEventsProcessor))]
        public async Task Run([Microsoft.Azure.Functions.Worker.QueueTrigger("authorizationeventlog", Connection = "QueueStorage")] string item, FunctionContext executionContext)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            AuthorizationEvent authorizationEvent = JsonSerializer.Deserialize<AuthorizationEvent>(item, options);
            await _auditLogClient.SaveAuthorizationEvent(authorizationEvent);

        }
    }
}
