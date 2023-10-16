namespace Altinn.Auth.AuditLog.Core.Enum
{
    /// <summary>
    /// Enumeration for authentication event types
    /// </summary>
    public enum AuthenticationEventType
    {
        Authenticate = 1,
        Refresh = 2,
        TokenExchange = 3,
        Logout = 4,
    }
}
