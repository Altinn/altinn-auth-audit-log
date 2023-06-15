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

        private readonly string insertAuthorizationEvent = "select * from authz.create_authorizationevent(@_subjectuserid,@_subjectparty,@_resourcepartyid,@_resource,@_instanceid,@_operation,@_timetodelete,@_ipadress,@_contextrequestjson)";

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

        public async Task<AuthorizationEvent> InsertAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            if (authorizationEvent == null) 
            {
                throw new ArgumentNullException(nameof(authorizationEvent));
            }

            var json = System.Text.Json.JsonSerializer.Serialize(authorizationEvent, new System.Text.Json.JsonSerializerOptions() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            try
            {
                await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                NpgsqlCommand pgcom = new NpgsqlCommand(insertAuthorizationEvent, conn);
                pgcom.Parameters.AddWithValue("_subjectuserid", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.SubjectUserId);
                pgcom.Parameters.AddWithValue("_subjectparty", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.SubjectParty);
                pgcom.Parameters.AddWithValue("_resourcepartyid", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.ResourcePartyId);
                pgcom.Parameters.AddWithValue("_resource", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.Resource);
                pgcom.Parameters.AddWithValue("_instanceid", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.InstanceId);
                pgcom.Parameters.AddWithValue("_operation", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.Operation);
                pgcom.Parameters.AddWithValue("_timetodelete", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.TimeToDelete);
                pgcom.Parameters.AddWithValue("_ipadress", NpgsqlTypes.NpgsqlDbType.Text, authorizationEvent.IpAdress);
                pgcom.Parameters.AddWithValue("_contextrequestjson", NpgsqlTypes.NpgsqlDbType.Jsonb, json);

                using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
                if (reader.Read())
                {
                    return GetAuthorizationEvent(reader);
                }

                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }

        private static AuthorizationEvent GetAuthorizationEvent(NpgsqlDataReader reader)
        {
            ContextRequest? contextRequest = null;
            if (reader["contextrequestjson"] != DBNull.Value)
            {
                var jsonb = reader.GetString("contextrequestjson");

                contextRequest = System.Text.Json.JsonSerializer.Deserialize<ContextRequest>(jsonb, new System.Text.Json.JsonSerializerOptions() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) as ContextRequest;
            }

            return new AuthorizationEvent
            {
                SubjectUserId = reader.GetFieldValue<string>("subjectuserid"),
                SubjectParty = reader.GetFieldValue<string>("subjectparty"),
                ResourcePartyId = reader.GetFieldValue<string>("resourcepartyid"),
                Resource = reader.GetFieldValue<string>("resource"),
                InstanceId = reader.GetFieldValue<string>("instanceid"),
                Operation = reader.GetFieldValue<string>("operation"),
                TimeToDelete = reader.GetFieldValue<string>("timetodelete"),
                IpAdress = reader.GetFieldValue<string>("ipadress"),
                ContextRequestJson = contextRequest
            };
            return null;
        }
    }
}
