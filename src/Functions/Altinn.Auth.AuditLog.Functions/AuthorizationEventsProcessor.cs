using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Microsoft.Azure.Functions.Worker;
using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Functions;

public class AuthorizationEventsProcessor
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

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
    public Task Run(
        [QueueTrigger("authorizationeventlog", Connection = "QueueStorage")] BinaryData item,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
    {
        var raw = item.ToMemory();
        var span = raw.Span;
        if (span.Length < 2)
        {
            return Task.CompletedTask;
        }

        var firstBytes = span[0..2];
        if (ushort.TryParse(firstBytes, NumberStyles.None, provider: null, out var version))
        {
            var rest = raw[2..];
            return version switch
            {
                _ => ProcessInvalidVersion(version, cancellationToken),
            };
        }

        return ProcessLegacyVersion(raw, cancellationToken); 
    }

    private async Task ProcessLegacyVersion(ReadOnlyMemory<byte> base64EncodedJson, CancellationToken cancellationToken)
    {
        // Data shrinks when decoded from base64, so the original length will fit the decoded byte array
        var raw = ArrayPool<byte>.Shared.Rent(base64EncodedJson.Length);

        try
        {
            System.Buffers.Text.Base64.DecodeFromUtf8(base64EncodedJson.Span, raw, out int bytesConsumed, out int bytesWritten);
            if (bytesConsumed != base64EncodedJson.Length)
            {
                throw new InvalidOperationException("Could not decode entire base64 input");
            }

            await _auditLogClient.SaveAuthorizationEvent(raw.AsMemory(0, bytesWritten), cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(raw);
        }
    }

    private async Task ProcessInvalidVersion(ushort version, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException($"Unsupported authorization event version: {version}");
    }
}
