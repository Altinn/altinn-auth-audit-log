using Altinn.Auth.AuditLog.Core.Enum;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Tests.Persistence;

/// <summary>
/// Verifies the <c>unnest</c>-based batch inserts against a real Postgres (Testcontainers).
/// </summary>
[Collection("AuthorizationEvent Tests")]
public class BatchInsertTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
    : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private IAuthorizationEventRepository AuthorizationRepository => Services.GetRequiredService<IAuthorizationEventRepository>();

    private IAuthenticationEventRepository AuthenticationRepository => Services.GetRequiredService<IAuthenticationEventRepository>();

    [Fact]
    public async Task InsertAuthorizationEvents_InsertsEveryRow_WithNullsPreserved()
    {
        // Partitions exist for the (fake) current month, so events must be dated accordingly.
        var created = TimeProvider.GetUtcNow();
        var marker = Guid.NewGuid().ToString("N");

        var events = new List<AuthorizationEvent>
        {
            NewAuthorizationEvent(created, marker, i: 0, withOptionalFields: true),
            NewAuthorizationEvent(created.AddSeconds(1), marker, i: 1, withOptionalFields: false),
            NewAuthorizationEvent(created.AddSeconds(2), marker, i: 2, withOptionalFields: true),
        };

        await AuthorizationRepository.InsertAuthorizationEvents(events);

        await using var cmd = DataSource.CreateCommand(
            /*strpsql*/"""
            SELECT subjectuserid, subjectorgcode, resource, instanceid, operation, ipaddress, decision, subject_party_uuid, trace_id, contextrequestjson->>'marker', created
            FROM authz.eventlogv1
            WHERE resource = @resource
            ORDER BY created
            """);
        cmd.Parameters.AddWithValue("resource", $"res-{marker}");

        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<(int? UserId, string? OrgCode, string? InstanceId, string Operation, string? Ip, int Decision, string? Uuid, string? TraceId, string Marker, DateTime Created)>();
        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.IsDBNull(0) ? null : reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetDateTime(10)));
        }

        Assert.Equal(3, rows.Count);

        // Row 0: all optional fields set
        Assert.Equal(2000000, rows[0].UserId);
        Assert.Equal("skd", rows[0].OrgCode);
        Assert.Equal("1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713", rows[0].InstanceId);
        Assert.Equal("read", rows[0].Operation);
        Assert.Equal("192.0.2.1", rows[0].Ip);
        Assert.Equal((int)XacmlContextDecision.Permit + 1, rows[0].Decision);
        Assert.Equal("732f9355-c0e4-4df8-98f0-8e773809ff63", rows[0].Uuid);
        Assert.Equal("trace-0", rows[0].TraceId);
        Assert.Equal(marker, rows[0].Marker);
        Assert.Equal(created.UtcDateTime, rows[0].Created, TimeSpan.FromMilliseconds(1));

        // Row 1: optional fields null (empty strings become NULL like the single insert does)
        Assert.Null(rows[1].UserId);
        Assert.Null(rows[1].OrgCode);
        Assert.Null(rows[1].InstanceId);
        Assert.Null(rows[1].Ip);
        Assert.Equal((int)XacmlContextDecision.Deny + 1, rows[1].Decision);
        Assert.Null(rows[1].Uuid);
        Assert.Null(rows[1].TraceId);
    }

    [Fact]
    public async Task InsertAuthorizationEvents_EmptyList_IsNoOp()
    {
        await AuthorizationRepository.InsertAuthorizationEvents([]);
    }

    [Fact]
    public async Task InsertAuthorizationEvents_InvalidEvent_ThrowsBeforeTouchingDb()
    {
        var created = TimeProvider.GetUtcNow();
        var marker = Guid.NewGuid().ToString("N");
        var events = new List<AuthorizationEvent>
        {
            NewAuthorizationEvent(created, marker, i: 0, withOptionalFields: true),
            new AuthorizationEvent { Created = created, Operation = "read", Decision = null, ContextRequestJson = JsonSerializer.Deserialize<JsonElement>("{}") },
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => AuthorizationRepository.InsertAuthorizationEvents(events));

        await using var cmd = DataSource.CreateCommand("SELECT count(*) FROM authz.eventlogv1 WHERE resource = @resource");
        cmd.Parameters.AddWithValue("resource", $"res-{marker}");
        Assert.Equal(0L, await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task InsertAuthenticationEvents_InsertsEveryRow()
    {
        var created = TimeProvider.GetUtcNow();
        var sessionId = Guid.NewGuid().ToString("N");

        var events = new List<AuthenticationEvent>
        {
            new()
            {
                SessionId = sessionId,
                Created = created,
                UserId = 20000003,
                EventType = AuthenticationEventType.Authenticate,
                AuthenticationMethod = AuthenticationMethod.BankID,
                AuthenticationLevel = SecurityLevel.VerySensitive,
                IpAddress = "192.0.2.7",
                IsAuthenticated = true,
            },
            new()
            {
                SessionId = sessionId,
                Created = created.AddSeconds(1),
                EventType = AuthenticationEventType.Logout,
                AuthenticationMethod = null,
                AuthenticationLevel = null,
                IsAuthenticated = false,
            },
        };

        await AuthenticationRepository.InsertAuthenticationEvents(events);

        await using var cmd = DataSource.CreateCommand(
            /*strpsql*/"""
            SELECT userid, eventtypeid, authenticationmethodid, authenticationlevelid, ipaddress, isauthenticated
            FROM authentication.eventlogv1
            WHERE sessionid = @sessionid
            ORDER BY created
            """);
        cmd.Parameters.AddWithValue("sessionid", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(20000003, reader.GetInt32(0));
        Assert.Equal((int)AuthenticationEventType.Authenticate, reader.GetInt32(1));
        Assert.Equal((int)AuthenticationMethod.BankID, reader.GetInt32(2));
        Assert.Equal((int)SecurityLevel.VerySensitive, reader.GetInt32(3));
        Assert.Equal("192.0.2.7", reader.GetString(4));
        Assert.True(reader.GetBoolean(5));

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
        Assert.Equal((int)AuthenticationEventType.Logout, reader.GetInt32(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(reader.IsDBNull(4));
        Assert.False(reader.GetBoolean(5));

        Assert.False(await reader.ReadAsync());
    }

    private static AuthorizationEvent NewAuthorizationEvent(DateTimeOffset created, string marker, int i, bool withOptionalFields)
    {
        return new AuthorizationEvent
        {
            SessionId = withOptionalFields ? $"session-{i}" : null,
            Created = created,
            SubjectUserId = withOptionalFields ? 2000000 : null,
            SubjectOrgCode = withOptionalFields ? "skd" : string.Empty,
            SubjectOrgNumber = withOptionalFields ? 974761076 : null,
            SubjectParty = withOptionalFields ? 50001337 : null,
            ResourcePartyId = withOptionalFields ? 1000 : null,
            Resource = $"res-{marker}",
            InstanceId = withOptionalFields ? "1000/26133fb5-a9f2-45d4-90b1-f6d93ad40713" : null,
            Operation = "read",
            IpAdress = withOptionalFields ? "192.0.2.1" : null,
            ContextRequestJson = JsonSerializer.Deserialize<JsonElement>($$"""{"marker":"{{marker}}","i":{{i}}}"""),
            Decision = withOptionalFields ? XacmlContextDecision.Permit : XacmlContextDecision.Deny,
            SubjectPartyUuid = withOptionalFields ? "732f9355-c0e4-4df8-98f0-8e773809ff63" : null,
            TraceId = withOptionalFields ? $"trace-{i}" : null,
        };
    }
}
