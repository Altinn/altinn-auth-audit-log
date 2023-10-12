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

        private readonly string insertAuthenticationEvent = "select * from authentication.create_authenticationevent(@_sessionid, @_created,@_userid,@_supplierid, @_orgnumber, @_eventtypeid, @_authenticationmethodid,@_authenticationlevelid,@_ipaddress,@_isauthenticated)";

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

        public async Task InsertAuthenticationEvent(AuthenticationEvent authenticationEvent)
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
                pgcom.Parameters.AddWithValue("_sessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SessionId) ? DBNull.Value : authenticationEvent.SessionId);
                pgcom.Parameters.AddWithValue("_created", NpgsqlTypes.NpgsqlDbType.Timestamp, authenticationEvent.Created == DateTime.MinValue ? DBNull.Value : authenticationEvent.Created);
                pgcom.Parameters.AddWithValue("_userid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.UserId == null) ? DBNull.Value : authenticationEvent.UserId);
                pgcom.Parameters.AddWithValue("_supplierid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SupplierId) ? DBNull.Value : authenticationEvent.SupplierId);
                pgcom.Parameters.AddWithValue("_orgnumber", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.OrgNumber == null) ? DBNull.Value : authenticationEvent.OrgNumber);
                pgcom.Parameters.AddWithValue("_eventtypeid", NpgsqlTypes.NpgsqlDbType.Integer, Convert.ToInt32(authenticationEvent.EventType));                
                pgcom.Parameters.AddWithValue("_authenticationmethodid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationMethod == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationMethod));
                pgcom.Parameters.AddWithValue("_authenticationlevelid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationLevel == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationLevel));
                pgcom.Parameters.AddWithValue("_ipaddress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.IpAddress) ? DBNull.Value : authenticationEvent.IpAddress);                
                pgcom.Parameters.AddWithValue("_isauthenticated", NpgsqlTypes.NpgsqlDbType.Boolean, authenticationEvent.IsAuthenticated);


                await pgcom.ExecuteReaderAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }        
    }
}
