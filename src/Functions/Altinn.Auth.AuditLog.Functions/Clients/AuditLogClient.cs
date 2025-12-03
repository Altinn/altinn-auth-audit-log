using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Functions.Clients;

/// <summary>
/// Integration to Auditlog api
/// </summary>
public class AuditLogClient : IAuditLogClient
{
    private static readonly MediaTypeHeaderValue _jsonContentType = new("application/json", charSet: "utf-8");
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

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
    public async Task SaveAuthenticationEvent(AuthenticationEvent authEvent, CancellationToken cancellationToken)
    {
        const string ENDPOINT_URL = "auditlog/api/v1/authenticationevent";

        using var content = JsonContent.Create(authEvent, options: _jsonSerializerOptions);
        var (success, statusCode) = await PostAuthEventToEndpoint(content, ENDPOINT_URL, cancellationToken);

        if (!success)
        {
            var msg = $"// SaveAuthenticationEvent failed with status code {statusCode}";
            _logger.LogError("SaveAuthenticationEvent failed with status code {statusCode}", statusCode);
            throw new HttpRequestException(msg);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAuthorizationEvent(ReadOnlySequence<byte> authorizationEvent, CancellationToken cancellationToken)
    {
        const string ENDPOINT_URL = "auditlog/api/v1/authorizationevent";

        using var stream = authorizationEvent.AsStream();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = _jsonContentType;

        var (success, statusCode) = await PostAuthEventToEndpoint(content, ENDPOINT_URL, cancellationToken);
        if (!success)
        {
            string msg = $"SaveAuthorizationEvent failed with status code {statusCode}";
            _logger.LogError("SaveAuthorizationEvent failed with status code {statusCode}", statusCode);
            throw new HttpRequestException(msg);
        }
    }

    private async Task<(bool Success, HttpStatusCode StatusCode)> PostAuthEventToEndpoint(HttpContent content, string endpoint, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _client.PostAsync(endpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (false, response.StatusCode);
        }

        return (true, response.StatusCode);
    }
}
