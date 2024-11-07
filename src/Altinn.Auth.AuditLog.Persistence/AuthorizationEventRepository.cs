using System.Diagnostics.CodeAnalysis;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

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
                decision
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
                @decision
            )
            RETURNING *;
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

            if (string.IsNullOrEmpty(authorizationEvent.ContextRequestJson))
            {
                throw new ArgumentNullException(nameof(authorizationEvent), "Context request must not be null or empty");
            }

            try
            {
                await using NpgsqlCommand pgcom = _dataSource.CreateCommand(INSERTAUTHZEVENT);
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
                pgcom.Parameters.AddWithValue("decision", NpgsqlTypes.NpgsqlDbType.Integer, Convert.ToInt32(authorizationEvent.Decision));

                await pgcom.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthorizationEvent // Exception");
                throw;
            }
        }
    }
}
