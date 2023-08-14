namespace Altinn.Auth.AuditLog.Functions.Configuration
{
    /// <summary>
    /// Represents a set of configuration options when communicating with the platform API.
    /// </summary>
    public class PlatformSettings
    {
        /// <summary>
        /// Gets or sets the url for the audit log API endpoint.
        /// </summary>
        public string AuditLogApiEndpoint { get; set; }
    }
}
