namespace Altinn.Auth.AuditLog.Persistence.Configuration
{
    /// <summary>
    /// Settings for Postgres database
    /// </summary>
    public class PostgreSQLSettings
    {
        /// <summary>
        /// Connection string for the postgres db
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Password for app user for the postgres db
        /// </summary>
        public string AuthAuditLogDbPwd { get; set; }

        /// <summary>
        /// Connection string for the postgres db
        /// </summary>
        public string AdminConnectionString { get; set; }

        /// <summary>
        /// Password for app user for the postgres db
        /// </summary>
        public string AuthAuditLogDbAdminPwd { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include parameter values in logging/tracing.
        /// </summary>
        public bool LogParameters { get; set; } = false;
    }
}
