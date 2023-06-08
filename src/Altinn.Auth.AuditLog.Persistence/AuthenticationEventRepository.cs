using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Auth.AuditLog.Persistence
{
    [ExcludeFromCodeCoverage]
    public class AuthenticationEventRepository : IAuthenticationEventRepository
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        private readonly string insertAuthenticationEvent = "select * from authentication.create_authenticationevent(@_authenticationeventjson)";

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationEventRepository"/> class
        /// </summary>
        /// <param name="postgresSettings">The postgreSQL configurations for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public AuthenticationEventRepository(
            IOptions<PostgreSQLSettings> postgresSettings,
            ILogger<AuthenticationEventRepository> logger) 
        { 
            _logger = logger;
            _connectionString = string.Format(
                                    postgresSettings.Value.ConnectionString,
                                    postgresSettings.Value.AuthAuditLogDbPwd);                                   
        }

        public async Task<AuthenticationEvent> InsertAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            if (authenticationEvent == null) 
            {
                throw new ArgumentNullException(nameof(authenticationEvent));
            }

            var json = System.Text.Json.JsonSerializer.Serialize(authenticationEvent, new System.Text.Json.JsonSerializerOptions() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            try
            {
                await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                NpgsqlCommand pgcom = new NpgsqlCommand(insertAuthenticationEvent, conn);
                pgcom.Parameters.AddWithValue("_authenticationeventjson", NpgsqlTypes.NpgsqlDbType.Jsonb, json);

                using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync();
                if (reader.Read())
                {
                    return GetAuthenticationEvent(reader);
                }

                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }

        private static AuthenticationEvent GetAuthenticationEvent(NpgsqlDataReader reader)
        {
            if (reader["authenticationeventjson"] != DBNull.Value)
            {
                var jsonb = reader.GetString("authenticationeventjson");

                AuthenticationEvent? authEvent = System.Text.Json.JsonSerializer.Deserialize<AuthenticationEvent>(jsonb, new System.Text.Json.JsonSerializerOptions() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) as AuthenticationEvent;
                return authEvent;
            }

            return null;
        }
    }
}
