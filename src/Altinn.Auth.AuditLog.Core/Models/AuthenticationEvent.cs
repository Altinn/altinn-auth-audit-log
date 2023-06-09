﻿using System.ComponentModel.DataAnnotations;

namespace Altinn.Auth.AuditLog.Core.Models
{
    /// <summary>
    /// This model describes an authentication event. An authentication event is an action triggered when a user authenticates to altinn
    /// </summary>
    public class AuthenticationEvent
    {
        /// <summary>
        /// Date, time of the authentication event. Set by producer of logevents
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Id of the user that triggered that authentication event 
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Relevant if the event is triggered by enterprise user
        /// </summary>
        public string? SupplierId { get; set; }

        /// <summary>
        /// The type of authentication event
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
