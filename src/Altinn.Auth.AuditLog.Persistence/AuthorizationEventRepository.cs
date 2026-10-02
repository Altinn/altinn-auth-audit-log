using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence.Extensions;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Altinn.Auth.AuditLog.Persistence
{
    [ExcludeFromCodeCoverage]
    public class AuthorizationEventRepository : IAuthorizationEventRepository
    {
        private readonly ILogger _logger;
        private readonly NpgsqlDataSource _dataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationEventRepository"/> class
        /// </summary>
        /// <param name="dataSource">The postgreSQL datasource for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public AuthorizationEventRepository(
            NpgsqlDataSource dataSource,
            ILogger<AuthorizationEventRepository> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        public async Task InsertAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            const string INSERTAUTHZEVENT = /*strpsql*/
            """
            INSERT INTO authz.eventlogv1(
                sessionid,
                created,
                subjectuserid,
                subjectorgcode,
                subjectorgnumber,
                subjectparty,
                resourcepartyid,
                resource,
                instanceid,
                operation,
                ipaddress,
                contextrequestjson,
                decision,
                subject_party_uuid,
                trace_id
            )
            VALUES (
                @sessionid,
                @created,
                @subjectuserid,
                @subjectorgcode,
                @subjectorgnumber,
                @subjectparty,
                @resourcepartyid,
                @resource,
                @instanceid,
                @operation,
                @ipaddress,
                @contextrequestjson,
                @decision,
                @subjectpartyuuid,
                @trace_id
            )
            """;

            if (authorizationEvent == null)
            {
                throw new ArgumentNullException(nameof(authorizationEvent));
            }

            if (!authorizationEvent.Created.HasValue)
            {
                throw new ArgumentNullException(nameof(authorizationEvent), "Created must not be null");
            }

            if (string.IsNullOrEmpty(authorizationEvent.Operation))
            {
                throw new ArgumentNullException(nameof(authorizationEvent), "Operation must not be null or empty");
            }

            if (authorizationEvent.ContextRequestJson.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentNullException(nameof(authorizationEvent), "Context request must be an object");
            }

            try
            {
                Debug.Assert(authorizationEvent.Decision.HasValue);
                var decisionValue = ((int)authorizationEvent.Decision.Value) + 1 /* db starts at value 1 */;
                Assert(decisionValue is >= 1 and <= 4);

                await using NpgsqlConnection pgcon = await _dataSource.OpenConnectionAsync();
                await using NpgsqlCommand pgcom = pgcon.CreateCommand(INSERTAUTHZEVENT);
                pgcom.Parameters.AddWithValue("sessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SessionId) ? DBNull.Value : authorizationEvent.SessionId);
                pgcom.Parameters.AddWithValue("created", NpgsqlTypes.NpgsqlDbType.TimestampTz, authorizationEvent.Created.Value.ToOffset(TimeSpan.Zero));
                pgcom.Parameters.AddWithValue("subjectuserid", NpgsqlTypes.NpgsqlDbType.Integer, (authorizationEvent.SubjectUserId == null) ? DBNull.Value : authorizationEvent.SubjectUserId);
                pgcom.Parameters.AddWithValue("subjectorgcode", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectOrgCode) ? DBNull.Value : authorizationEvent.SubjectOrgCode);
                pgcom.Parameters.AddWithValue("subjectorgnumber", NpgsqlTypes.NpgsqlDbType.Integer, (authorizationEvent.SubjectOrgNumber == null) ? DBNull.Value : authorizationEvent.SubjectOrgNumber);
                pgcom.Parameters.AddWithValue("subjectparty", NpgsqlTypes.NpgsqlDbType.Integer, (authorizationEvent.SubjectParty == null) ? DBNull.Value : authorizationEvent.SubjectParty);
                pgcom.Parameters.AddWithValue("resourcepartyid", NpgsqlTypes.NpgsqlDbType.Integer, (authorizationEvent.ResourcePartyId == null) ? DBNull.Value : authorizationEvent.ResourcePartyId);
                pgcom.Parameters.AddWithValue("resource", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.Resource) ? DBNull.Value : authorizationEvent.Resource);
                pgcom.Parameters.AddWithValue("instanceid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.InstanceId) ? DBNull.Value : authorizationEvent.InstanceId);
                pgcom.Parameters.AddWithValue("operation", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.Operation) ? DBNull.Value : authorizationEvent.Operation);
                pgcom.Parameters.AddWithValue("ipaddress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.IpAdress) ? DBNull.Value : authorizationEvent.IpAdress);
                pgcom.Parameters.AddWithValue("contextrequestjson", NpgsqlTypes.NpgsqlDbType.Jsonb, authorizationEvent.ContextRequestJson);
                pgcom.Parameters.AddWithValue("decision", NpgsqlTypes.NpgsqlDbType.Integer, decisionValue);
                pgcom.Parameters.AddWithValue("subjectpartyuuid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectPartyUuid) ? DBNull.Value : authorizationEvent.SubjectPartyUuid);
                pgcom.Parameters.AddWithValue("trace_id", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.TraceId) ? DBNull.Value : authorizationEvent.TraceId);

                await pgcom.PrepareAsync();
                await pgcom.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthorizationEvent // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task InsertAuthorizationEvents(IReadOnlyList<AuthorizationEvent> authorizationEvents, CancellationToken cancellationToken = default)
        {
            // One statement == one transaction: either every row is inserted, or none are.
            // unnest over typed array parameters keeps this a single prepared statement regardless of batch size,
            // and (unlike COPY) leaves room for ON CONFLICT when dedup lands.
            const string INSERTAUTHZEVENTS = /*strpsql*/
            """
            INSERT INTO authz.eventlogv1(
                sessionid,
                created,
                subjectuserid,
                subjectorgcode,
                subjectorgnumber,
                subjectparty,
                resourcepartyid,
                resource,
                instanceid,
                operation,
                ipaddress,
                contextrequestjson,
                decision,
                subject_party_uuid,
                trace_id
            )
            SELECT * FROM unnest(
                @sessionid,
                @created,
                @subjectuserid,
                @subjectorgcode,
                @subjectorgnumber,
                @subjectparty,
                @resourcepartyid,
                @resource,
                @instanceid,
                @operation,
                @ipaddress,
                @contextrequestjson,
                @decision,
                @subjectpartyuuid,
                @trace_id
            )
            """;

            ArgumentNullException.ThrowIfNull(authorizationEvents);
            if (authorizationEvents.Count == 0)
            {
                return;
            }

            var count = authorizationEvents.Count;
            var sessionId = new string?[count];
            var created = new DateTime[count];
            var subjectUserId = new int?[count];
            var subjectOrgCode = new string?[count];
            var subjectOrgNumber = new int?[count];
            var subjectParty = new int?[count];
            var resourcePartyId = new int?[count];
            var resource = new string?[count];
            var instanceId = new string?[count];
            var operation = new string?[count];
            var ipAddress = new string?[count];
            var contextRequestJson = new string[count];
            var decision = new int[count];
            var subjectPartyUuid = new string?[count];
            var traceId = new string?[count];

            for (var i = 0; i < count; i++)
            {
                var authorizationEvent = authorizationEvents[i];
                if (authorizationEvent is null)
                {
                    throw new ArgumentNullException(nameof(authorizationEvents), $"Event at index {i} is null");
                }

                if (!authorizationEvent.Created.HasValue)
                {
                    throw new ArgumentNullException(nameof(authorizationEvents), $"Event at index {i}: Created must not be null");
                }

                if (string.IsNullOrEmpty(authorizationEvent.Operation))
                {
                    throw new ArgumentNullException(nameof(authorizationEvents), $"Event at index {i}: Operation must not be null or empty");
                }

                if (authorizationEvent.ContextRequestJson.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentNullException(nameof(authorizationEvents), $"Event at index {i}: Context request must be an object");
                }

                if (!authorizationEvent.Decision.HasValue)
                {
                    throw new ArgumentNullException(nameof(authorizationEvents), $"Event at index {i}: Decision must not be null");
                }

                var decisionValue = ((int)authorizationEvent.Decision.Value) + 1 /* db starts at value 1 */;
                Assert(decisionValue is >= 1 and <= 4);

                sessionId[i] = NullIfEmpty(authorizationEvent.SessionId);
                created[i] = authorizationEvent.Created.Value.UtcDateTime;
                subjectUserId[i] = authorizationEvent.SubjectUserId;
                subjectOrgCode[i] = NullIfEmpty(authorizationEvent.SubjectOrgCode);
                subjectOrgNumber[i] = authorizationEvent.SubjectOrgNumber;
                subjectParty[i] = authorizationEvent.SubjectParty;
                resourcePartyId[i] = authorizationEvent.ResourcePartyId;
                resource[i] = NullIfEmpty(authorizationEvent.Resource);
                instanceId[i] = NullIfEmpty(authorizationEvent.InstanceId);
                operation[i] = authorizationEvent.Operation;
                ipAddress[i] = NullIfEmpty(authorizationEvent.IpAdress);
                contextRequestJson[i] = authorizationEvent.ContextRequestJson.GetRawText();
                decision[i] = decisionValue;
                subjectPartyUuid[i] = NullIfEmpty(authorizationEvent.SubjectPartyUuid);
                traceId[i] = NullIfEmpty(authorizationEvent.TraceId);
            }

            await using NpgsqlConnection pgcon = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlCommand pgcom = pgcon.CreateCommand(INSERTAUTHZEVENTS);

            pgcom.Parameters.Add<string?[]>("sessionid", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = sessionId;
            pgcom.Parameters.Add<DateTime[]>("created", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz).TypedValue = created;
            pgcom.Parameters.Add<int?[]>("subjectuserid", NpgsqlDbType.Array | NpgsqlDbType.Integer).TypedValue = subjectUserId;
            pgcom.Parameters.Add<string?[]>("subjectorgcode", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = subjectOrgCode;
            pgcom.Parameters.Add<int?[]>("subjectorgnumber", NpgsqlDbType.Array | NpgsqlDbType.Integer).TypedValue = subjectOrgNumber;
            pgcom.Parameters.Add<int?[]>("subjectparty", NpgsqlDbType.Array | NpgsqlDbType.Integer).TypedValue = subjectParty;
            pgcom.Parameters.Add<int?[]>("resourcepartyid", NpgsqlDbType.Array | NpgsqlDbType.Integer).TypedValue = resourcePartyId;
            pgcom.Parameters.Add<string?[]>("resource", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = resource;
            pgcom.Parameters.Add<string?[]>("instanceid", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = instanceId;
            pgcom.Parameters.Add<string?[]>("operation", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = operation;
            pgcom.Parameters.Add<string?[]>("ipaddress", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = ipAddress;
            pgcom.Parameters.Add<string[]>("contextrequestjson", NpgsqlDbType.Array | NpgsqlDbType.Jsonb).TypedValue = contextRequestJson;
            pgcom.Parameters.Add<int[]>("decision", NpgsqlDbType.Array | NpgsqlDbType.Integer).TypedValue = decision;
            pgcom.Parameters.Add<string?[]>("subjectpartyuuid", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = subjectPartyUuid;
            pgcom.Parameters.Add<string?[]>("trace_id", NpgsqlDbType.Array | NpgsqlDbType.Text).TypedValue = traceId;

            await pgcom.PrepareAsync(cancellationToken);
            var inserted = await pgcom.ExecuteNonQueryAsync(cancellationToken);
            if (inserted != count)
            {
                throw new InvalidOperationException($"Expected to insert {count} authorization events, but {inserted} rows were affected");
            }
        }

        private static string? NullIfEmpty(string? value)
            => string.IsNullOrEmpty(value) ? null : value;

        private static void Assert(
            [DoesNotReturnIf(false)] bool condition,
            [CallerArgumentExpression(nameof(condition))] string? message = null)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message ?? "Assert condition failed");
            }
        }
    }
}
