using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Auth.AuditLog.Persistence
{
    [ExcludeFromCodeCoverage]
    public class AuthorizationEventRepository : IAuthorizationEventRepository
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        private readonly string insertAuthorizationEvent = "select * from authz.create_authorizationevent(@_subjectuserid,@_subjectorgcode,@_subjectorgnumber,@_subjectparty,@_resourcepartyid,@_resource,@_instanceid,@_operation,@_timetodelete,@_ipadress,@_contextrequestjson,@_decision)";

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationEventRepository"/> class
        /// </summary>
        /// <param name="postgresSettings">The postgreSQL configurations for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public AuthorizationEventRepository(
            IOptions<PostgreSQLSettings> postgresSettings,
            ILogger<AuthorizationEventRepository> logger) 
        { 
            _logger = logger;
            _connectionString = string.Format(
                                    postgresSettings.Value.ConnectionString,
                                    postgresSettings.Value.AuthAuditLogDbPwd);                                   
        }

        public async Task InsertAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            if (authorizationEvent == null) 
            {
                throw new ArgumentNullException(nameof(authorizationEvent));
            }
          
            try
            {
                await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                NpgsqlCommand pgcom = new NpgsqlCommand(insertAuthorizationEvent, conn);
                pgcom.Parameters.AddWithValue("_subjectuserid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectUserId) ? DBNull.Value : authorizationEvent.SubjectUserId);
                pgcom.Parameters.AddWithValue("_subjectorgcode", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectOrgCode) ? DBNull.Value : authorizationEvent.SubjectOrgCode);
                pgcom.Parameters.AddWithValue("_subjectorgnumber", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectOrgNumber) ? DBNull.Value : authorizationEvent.SubjectOrgNumber);
                pgcom.Parameters.AddWithValue("_subjectparty", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.SubjectParty) ? DBNull.Value : authorizationEvent.SubjectParty);
                pgcom.Parameters.AddWithValue("_resourcepartyid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.ResourcePartyId) ? DBNull.Value : authorizationEvent.ResourcePartyId);
                pgcom.Parameters.AddWithValue("_resource", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.Resource) ? DBNull.Value : authorizationEvent.Resource);
                pgcom.Parameters.AddWithValue("_instanceid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.InstanceId) ? DBNull.Value : authorizationEvent.InstanceId);
                pgcom.Parameters.AddWithValue("_operation", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.Operation) ? DBNull.Value : authorizationEvent.Operation);
                pgcom.Parameters.AddWithValue("_timetodelete", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.TimeToDelete) ? DBNull.Value : authorizationEvent.TimeToDelete);
                pgcom.Parameters.AddWithValue("_ipadress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.IpAdress) ? DBNull.Value : authorizationEvent.IpAdress);
                pgcom.Parameters.AddWithValue("_contextrequestjson", NpgsqlTypes.NpgsqlDbType.Jsonb, authorizationEvent.ContextRequestJson);
                pgcom.Parameters.AddWithValue("_decision", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authorizationEvent.Decision) ? DBNull.Value : authorizationEvent.Decision);

                using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();                
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }
    }
}
