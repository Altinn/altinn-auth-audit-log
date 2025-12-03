using System;
using Azure.Messaging;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using System.Text.Json.Serialization;
using Altinn.Auth.AuditLog.Core.Models;

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
        public async Task Run(
            [QueueTrigger("eventlog", Connection = "QueueStorage")] string item,
            FunctionContext executionContext,
            CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            AuthenticationEvent authEvent = JsonSerializer.Deserialize<AuthenticationEvent>(item, options);
            await _auditLogClient.SaveAuthenticationEvent(authEvent, cancellationToken);

        }
    }
}
