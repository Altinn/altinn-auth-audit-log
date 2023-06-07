using System.ComponentModel.DataAnnotations;

namespace Altinn.Auth.AuditLog.Core.Models
{
    /// <summary>
    /// This model describes an authentication event. An authentication event is an action triggered when a user authenticates to altinn
    /// </summary>
    public class AuthorizationEvent
    {
        /// <summary>
        /// Date, time of the authorization event. Set by producer of logevents
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Id of the user that triggered that authorization event 
        /// </summary>
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// Relevant if the event is triggered by enterprise user
        /// </summary>
        public string? SupplierId { get; set; }

        /// <summary>
        /// The type of authorization event
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Relevant if the event is triggered by enterprise user?
        /// </summary>
        public string? OrgNumber { get; set; }

        /// <summary>
        /// The type of authentication used by the user (BankId etc)
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// The level of authentication used by the user (1, 2,  etc)
        /// </summary>
        public string AuthenticationLevel { get; set; }

        /// <summary>
        /// The session id
        /// </summary>
        public string SessionId { get; set; }
    }
}
