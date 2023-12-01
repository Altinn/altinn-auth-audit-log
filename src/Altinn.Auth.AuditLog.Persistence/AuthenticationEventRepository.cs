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
        private readonly NpgsqlDataSource _dataSource;        

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationEventRepository"/> class
        /// </summary>
        /// <param name="postgresSettings">The postgreSQL configurations for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public AuthenticationEventRepository(
            NpgsqlDataSource dataSource,
            ILogger<AuthenticationEventRepository> logger) 
        {
            _dataSource = dataSource;
            _logger = logger;                                 
        }

        public async Task InsertAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            const string INSERTAUTHNEVENT = /*strpsql*/@"
            INSERT INTO authentication.eventlog(
	        sessionid,
	        externalsessionid,
	        subscriptionkey,
	        externaltokenissuer,
	        created,
	        userid,
	        supplierid,
	        orgnumber,
	        eventtypeid,	
	        authenticationmethodid,
	        authenticationlevelid,
	        ipaddress,
	        isauthenticated
            )
            VALUES (
	            @sessionid,
	            @externalsessionid,
	            @subscriptionkey,
	            @externaltokenissuer,
	            @created,
	            @userid,
	            @supplierid,
	            @orgnumber,
	            @eventtypeid,	
	            @authenticationmethodid,
	            @authenticationlevelid,
	            @ipaddress,
	            @isauthenticated
            )
            RETURNING *;";

            if (authenticationEvent == null) 
            {
                throw new ArgumentNullException(nameof(authenticationEvent));
            }
          
            try
            {
                await using NpgsqlCommand pgcom = _dataSource.CreateCommand(INSERTAUTHNEVENT);
                
                pgcom.Parameters.AddWithValue("sessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SessionId) ? DBNull.Value : authenticationEvent.SessionId);
                pgcom.Parameters.AddWithValue("externalsessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.ExternalSessionId) ? DBNull.Value : authenticationEvent.ExternalSessionId);
                pgcom.Parameters.AddWithValue("subscriptionkey", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SubscriptionKey) ? DBNull.Value : authenticationEvent.SubscriptionKey);
                pgcom.Parameters.AddWithValue("externaltokenissuer", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.ExternalTokenIssuer) ? DBNull.Value : authenticationEvent.ExternalTokenIssuer);
                pgcom.Parameters.AddWithValue("created", NpgsqlTypes.NpgsqlDbType.TimestampTz, authenticationEvent.Created == DateTime.MinValue ? DBNull.Value : authenticationEvent.Created);
                pgcom.Parameters.AddWithValue("userid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.UserId == null) ? DBNull.Value : authenticationEvent.UserId);
                pgcom.Parameters.AddWithValue("supplierid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SupplierId) ? DBNull.Value : authenticationEvent.SupplierId);
                pgcom.Parameters.AddWithValue("orgnumber", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.OrgNumber == null) ? DBNull.Value : authenticationEvent.OrgNumber);
                pgcom.Parameters.AddWithValue("eventtypeid", NpgsqlTypes.NpgsqlDbType.Integer, Convert.ToInt32(authenticationEvent.EventType));                
                pgcom.Parameters.AddWithValue("authenticationmethodid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationMethod == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationMethod));
                pgcom.Parameters.AddWithValue("authenticationlevelid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationLevel == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationLevel));
                pgcom.Parameters.AddWithValue("ipaddress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.IpAddress) ? DBNull.Value : authenticationEvent.IpAddress);                
                pgcom.Parameters.AddWithValue("isauthenticated", NpgsqlTypes.NpgsqlDbType.Boolean, authenticationEvent.IsAuthenticated);


                await pgcom.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }        
    }
}
