using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using CommunityToolkit.HighPerformance;
using Microsoft.Azure.Functions.Worker;
using Microsoft.IO;
using System.Buffers;
using System.Globalization;
using System.IO.Compression;

namespace Altinn.Auth.AuditLog.Functions;

public class AuthorizationEventsProcessor
{
    private static readonly RecyclableMemoryStreamManager _manager = new();

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
                01 => ProcessV01(rest, cancellationToken),
                _ => ProcessInvalidVersion(version, cancellationToken),
            };
        }

        return ProcessLegacyVersion(raw, cancellationToken); 
    }

    // brotli encoded JSON
    private async Task ProcessV01(ReadOnlyMemory<byte> binaryData, CancellationToken cancellationToken)
    {
        using var jsonStream = _manager.GetStream(nameof(ProcessV01));

        {
            using var receivedStream = binaryData.AsStream();
            using var decodedStream = new BrotliStream(receivedStream, CompressionMode.Decompress);
            decodedStream.CopyTo(jsonStream); // No point in doing async here, as everything is in-memory at this point.
        }

        await _auditLogClient.SaveAuthorizationEvent(jsonStream.GetReadOnlySequence(), cancellationToken);
    }

    private async Task ProcessLegacyVersion(ReadOnlyMemory<byte> base64EncodedJson, CancellationToken cancellationToken)
    {
        // Check if data starts with `{`, if it does it's not base64 encoded
        if (base64EncodedJson.Span[0] == '{')
        {
            var sequence = new ReadOnlySequence<byte>(base64EncodedJson);
            await _auditLogClient.SaveAuthorizationEvent(sequence, cancellationToken);
            return;
        }

        // Data shrinks when decoded from base64, so the original length will fit the decoded byte array
        var raw = ArrayPool<byte>.Shared.Rent(base64EncodedJson.Length);

        try
        {
            System.Buffers.Text.Base64.DecodeFromUtf8(base64EncodedJson.Span, raw, out int bytesConsumed, out int bytesWritten);
            if (bytesConsumed != base64EncodedJson.Length)
            {
                throw new InvalidOperationException($"Could not decode entire base64 input (bytes consumed: {bytesConsumed}, input length: {base64EncodedJson.Length})");
            }

            var sequence = new ReadOnlySequence<byte>(raw.AsMemory(0, bytesWritten));
            await _auditLogClient.SaveAuthorizationEvent(sequence, cancellationToken);
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
