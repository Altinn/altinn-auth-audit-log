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

        private readonly string insertAuthenticationEvent = "select * from authentication.create_authenticationevent(@_created,@_userid,@_supplierid,@_eventtype,@_orgnumber,@_authenticationmethod,@_authenticationlevel,@_ipaddress,@_isauthenticated,@_timetodelete)";

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
                pgcom.Parameters.AddWithValue("_created", NpgsqlTypes.NpgsqlDbType.Timestamp, authenticationEvent.Created == DateTime.MinValue ? DBNull.Value : authenticationEvent.Created);
                pgcom.Parameters.AddWithValue("_userid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.UserId == null) ? DBNull.Value : authenticationEvent.UserId);
                pgcom.Parameters.AddWithValue("_supplierid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SupplierId) ? DBNull.Value : authenticationEvent.SupplierId);
                pgcom.Parameters.AddWithValue("_eventtype", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.EventType) ? DBNull.Value : authenticationEvent.EventType);
                pgcom.Parameters.AddWithValue("_orgnumber", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.OrgNumber == null) ? DBNull.Value : authenticationEvent.OrgNumber);
                pgcom.Parameters.AddWithValue("_authenticationmethod", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.AuthenticationMethod) ? DBNull.Value : authenticationEvent.AuthenticationMethod);
                pgcom.Parameters.AddWithValue("_authenticationlevel", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.AuthenticationLevel) ? DBNull.Value : authenticationEvent.AuthenticationLevel);
                pgcom.Parameters.AddWithValue("_ipaddress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.IpAddress) ? DBNull.Value : authenticationEvent.IpAddress);                
                pgcom.Parameters.AddWithValue("_isauthenticated", NpgsqlTypes.NpgsqlDbType.Boolean, authenticationEvent.IsAuthenticated);
                pgcom.Parameters.AddWithValue("_timetodelete", NpgsqlTypes.NpgsqlDbType.Timestamp, authenticationEvent.TimeToDelete == DateTime.MinValue ? DBNull.Value : authenticationEvent.TimeToDelete);


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
