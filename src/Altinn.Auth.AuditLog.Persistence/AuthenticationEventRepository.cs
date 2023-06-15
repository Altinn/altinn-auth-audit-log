using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
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

        private readonly string insertAuthenticationEvent = "select * from authentication.create_authenticationevent(@_userid,@_supplierid,@_eventtype,@_orgnumber,@_authenticationmethod,@_authenticationlevel,@_sessionid)";

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
                pgcom.Parameters.AddWithValue("_userid", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.UserId);
                pgcom.Parameters.AddWithValue("_supplierid", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.SupplierId);
                pgcom.Parameters.AddWithValue("_eventtype", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.EventType);
                pgcom.Parameters.AddWithValue("_orgnumber", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.OrgNumber);
                pgcom.Parameters.AddWithValue("_authenticationmethod", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.AuthenticationMethod);
                pgcom.Parameters.AddWithValue("_authenticationlevel", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.AuthenticationLevel);
                pgcom.Parameters.AddWithValue("_sessionid", NpgsqlTypes.NpgsqlDbType.Text, authenticationEvent.SessionId);

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
            return new AuthenticationEvent
            {
                Created = reader.GetFieldValue<DateTime>("created"),
                UserId = reader.GetFieldValue<string>("userid"),
                SupplierId = reader.GetFieldValue<string>("supplierid"),
                EventType = reader.GetFieldValue<string>("eventtype"),
                OrgNumber = reader.GetFieldValue<string>("orgnumber"),
                AuthenticationMethod = reader.GetFieldValue<string>("authenticationmethod"),
                AuthenticationLevel = reader.GetFieldValue<string>("authenticationlevel"),
                SessionId = reader.GetFieldValue<string>("sessionid")
            };
        }
    }
}
