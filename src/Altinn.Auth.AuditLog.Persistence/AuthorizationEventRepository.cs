using System;
using System.Collections.Generic;
using System.Data;
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
    public class AuthorizationEventRepository : IAuthorizationEventRepository
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        private readonly string insertAuthorizationEvent = "select * from authorization.create_authorizationevent(@_authorizationeventjson)";

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
                pgcom.Parameters.AddWithValue("_authenticationeventjson", NpgsqlTypes.NpgsqlDbType.Jsonb, json);

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
            if (reader["authorizationeventjson"] != DBNull.Value)
            {
                var jsonb = reader.GetString("authorizationeventjson");

                AuthorizationEvent? authEvent = System.Text.Json.JsonSerializer.Deserialize<AuthorizationEvent>(jsonb, new System.Text.Json.JsonSerializerOptions() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) as AuthorizationEvent;
                return authEvent;
            }

            return null;
        }
    }
}
