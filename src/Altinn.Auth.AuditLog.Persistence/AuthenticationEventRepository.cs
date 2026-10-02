using System.Diagnostics.CodeAnalysis;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

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
        /// <param name="dataSource">The postgreSQL datasource for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public AuthenticationEventRepository(NpgsqlDataSource dataSource,
            ILogger<AuthenticationEventRepository> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task InsertAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            const string INSERTAUTHNEVENT = /*strpsql*/
            """
            INSERT INTO authentication.eventlogv1 (
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
            RETURNING *;
            """;

            if (authenticationEvent == null)
            {
                throw new ArgumentNullException(nameof(authenticationEvent));
            }

            if (!authenticationEvent.Created.HasValue)
            {
                throw new ArgumentNullException(nameof(AuthenticationEvent.Created));
            }

            try
            {
                await using NpgsqlConnection pgcon = await _dataSource.OpenConnectionAsync();
                await using NpgsqlCommand pgcom = pgcon.CreateCommand(INSERTAUTHNEVENT);

                pgcom.Parameters.AddWithValue("sessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SessionId) ? DBNull.Value : authenticationEvent.SessionId);
                pgcom.Parameters.AddWithValue("externalsessionid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.ExternalSessionId) ? DBNull.Value : authenticationEvent.ExternalSessionId);
                pgcom.Parameters.AddWithValue("subscriptionkey", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SubscriptionKey) ? DBNull.Value : authenticationEvent.SubscriptionKey);
                pgcom.Parameters.AddWithValue("externaltokenissuer", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.ExternalTokenIssuer) ? DBNull.Value : authenticationEvent.ExternalTokenIssuer);
                pgcom.Parameters.AddWithValue("created", NpgsqlTypes.NpgsqlDbType.TimestampTz, authenticationEvent.Created.Value.ToOffset(TimeSpan.Zero));
                pgcom.Parameters.AddWithValue("userid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.UserId == null) ? DBNull.Value : authenticationEvent.UserId);
                pgcom.Parameters.AddWithValue("supplierid", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.SupplierId) ? DBNull.Value : authenticationEvent.SupplierId);
                pgcom.Parameters.AddWithValue("orgnumber", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.OrgNumber == null) ? DBNull.Value : authenticationEvent.OrgNumber);
                pgcom.Parameters.AddWithValue("eventtypeid", NpgsqlTypes.NpgsqlDbType.Integer, Convert.ToInt32(authenticationEvent.EventType));
                pgcom.Parameters.AddWithValue("authenticationmethodid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationMethod == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationMethod));
                pgcom.Parameters.AddWithValue("authenticationlevelid", NpgsqlTypes.NpgsqlDbType.Integer, (authenticationEvent.AuthenticationLevel == null) ? DBNull.Value : Convert.ToInt32(authenticationEvent.AuthenticationLevel));
                pgcom.Parameters.AddWithValue("ipaddress", NpgsqlTypes.NpgsqlDbType.Text, string.IsNullOrEmpty(authenticationEvent.IpAddress) ? DBNull.Value : authenticationEvent.IpAddress);
                pgcom.Parameters.AddWithValue("isauthenticated", NpgsqlTypes.NpgsqlDbType.Boolean, authenticationEvent.IsAuthenticated);

                await pgcom.PrepareAsync();
                await pgcom.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuditLogMetadataRepository // InsertAuthenticationEvent // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task InsertAuthenticationEvents(IReadOnlyList<AuthenticationEvent> authenticationEvents, CancellationToken cancellationToken = default)
        {
            // One statement == one transaction: either every row is inserted, or none are.
            const string INSERTAUTHNEVENTS = /*strpsql*/
            """
            INSERT INTO authentication.eventlogv1 (
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
            SELECT * FROM unnest(
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
            """;

            ArgumentNullException.ThrowIfNull(authenticationEvents);
            if (authenticationEvents.Count == 0)
            {
                return;
            }

            var count = authenticationEvents.Count;
            var sessionId = new string?[count];
            var externalSessionId = new string?[count];
            var subscriptionKey = new string?[count];
            var externalTokenIssuer = new string?[count];
            var created = new DateTime[count];
            var userId = new int?[count];
            var supplierId = new string?[count];
            var orgNumber = new int?[count];
            var eventTypeId = new int[count];
            var authenticationMethodId = new int?[count];
            var authenticationLevelId = new int?[count];
            var ipAddress = new string?[count];
            var isAuthenticated = new bool[count];

            for (var i = 0; i < count; i++)
            {
                var authenticationEvent = authenticationEvents[i];
                if (authenticationEvent is null)
                {
                    throw new ArgumentNullException(nameof(authenticationEvents), $"Event at index {i} is null");
                }

                if (!authenticationEvent.Created.HasValue)
                {
                    throw new ArgumentNullException(nameof(authenticationEvents), $"Event at index {i}: Created must not be null");
                }

                sessionId[i] = NullIfEmpty(authenticationEvent.SessionId);
                externalSessionId[i] = NullIfEmpty(authenticationEvent.ExternalSessionId);
                subscriptionKey[i] = NullIfEmpty(authenticationEvent.SubscriptionKey);
                externalTokenIssuer[i] = NullIfEmpty(authenticationEvent.ExternalTokenIssuer);
                created[i] = authenticationEvent.Created.Value.UtcDateTime;
                userId[i] = authenticationEvent.UserId;
                supplierId[i] = NullIfEmpty(authenticationEvent.SupplierId);
                orgNumber[i] = authenticationEvent.OrgNumber;
                eventTypeId[i] = Convert.ToInt32(authenticationEvent.EventType);
                authenticationMethodId[i] = authenticationEvent.AuthenticationMethod is null ? null : Convert.ToInt32(authenticationEvent.AuthenticationMethod);
                authenticationLevelId[i] = authenticationEvent.AuthenticationLevel is null ? null : Convert.ToInt32(authenticationEvent.AuthenticationLevel);
                ipAddress[i] = NullIfEmpty(authenticationEvent.IpAddress);
                isAuthenticated[i] = authenticationEvent.IsAuthenticated;
            }

            try
            {
                await using NpgsqlConnection pgcon = await _dataSource.OpenConnectionAsync(cancellationToken);
                await using NpgsqlCommand pgcom = pgcon.CreateCommand(INSERTAUTHNEVENTS);

                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("sessionid", sessionId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("externalsessionid", externalSessionId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("subscriptionkey", subscriptionKey) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("externaltokenissuer", externalTokenIssuer) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<DateTime[]>("created", created) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.TimestampTz });
                pgcom.Parameters.Add(new NpgsqlParameter<int?[]>("userid", userId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("supplierid", supplierId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<int?[]>("orgnumber", orgNumber) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                pgcom.Parameters.Add(new NpgsqlParameter<int[]>("eventtypeid", eventTypeId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                pgcom.Parameters.Add(new NpgsqlParameter<int?[]>("authenticationmethodid", authenticationMethodId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                pgcom.Parameters.Add(new NpgsqlParameter<int?[]>("authenticationlevelid", authenticationLevelId) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                pgcom.Parameters.Add(new NpgsqlParameter<string?[]>("ipaddress", ipAddress) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                pgcom.Parameters.Add(new NpgsqlParameter<bool[]>("isauthenticated", isAuthenticated) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Boolean });

                await pgcom.PrepareAsync(cancellationToken);
                var inserted = await pgcom.ExecuteNonQueryAsync(cancellationToken);
                if (inserted != count)
                {
                    throw new InvalidOperationException($"Expected to insert {count} authentication events, but {inserted} rows were affected");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // AuthenticationEventRepository // InsertAuthenticationEvents // Exception (batch size {BatchSize})", count);
                throw;
            }
        }

        private static string? NullIfEmpty(string? value)
            => string.IsNullOrEmpty(value) ? null : value;
    }
}
