using System;
using Azure.Messaging;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Altinn.Auth.AuditLog.Functions.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog.Functions
{
    public class EventsProcessor
    {
        private readonly ILogger _logger;
        private readonly IAuditLogClient _auditLogClient;

        public EventsProcessor(ILogger<EventsProcessor> logger,
            IAuditLogClient auditLogClient)
        {
            _logger = logger;
            _auditLogClient = auditLogClient;
        }

        /// <summary>
        /// Reads cloud event from eventlog queue and post it to auditlog api
        /// </summary>
        [Function(nameof(EventsProcessor))]
        public async Task Run([Microsoft.Azure.Functions.Worker.QueueTrigger("eventlog", Connection = "QueueStorage")] string item, FunctionContext executionContext)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            AuthenticationEvent authEvent = JsonSerializer.Deserialize<AuthenticationEvent>(item, options);
            await _auditLogClient.SaveAuthenticationEvent(authEvent);

        }
    }
}
