using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Functions
{
    public class AuthorizationEventsProcessor
    {
        private readonly IAuditLogClient _auditLogClient;

        public AuthorizationEventsProcessor(
            IAuditLogClient auditLogClient)
        {
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
            options.Converters.Add(new JsonStringEnumConverter());
            AuthorizationEvent? authorizationEvent = JsonSerializer.Deserialize<AuthorizationEvent>(item, options);
            await _auditLogClient.SaveAuthorizationEvent(authorizationEvent);
        }
    }
}
