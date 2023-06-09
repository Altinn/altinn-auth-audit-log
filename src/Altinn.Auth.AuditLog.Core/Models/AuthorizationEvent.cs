﻿using System.ComponentModel.DataAnnotations;

namespace Altinn.Auth.AuditLog.Core.Models
{
    /// <summary>
    /// This model describes an authorization event. An authorization event is an action triggered when a user requests access to an operation
    /// </summary>
    public class AuthorizationEvent
    {
        /// <summary>
        /// Date, time of the authorization event. Set by producer of logevents
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// The userid for the user that requested authorization
        /// </summary>
        public string SubjectUserId { get; set; }

        /// <summary>
        /// The partyid for the user that requested authorization
        /// </summary>
        public string? SubjectParty { get; set; }

        /// <summary>
        /// The partyId for resource owner when applicable
        /// </summary>
        public string ResourcePartyId { get; set; }

        /// <summary>
        /// The Main resource Id (app, external resource +)
        /// </summary>
        public string? Resource { get; set; }

        /// <summary>
        /// Instance Id when applicable
        /// </summary>
        public string InstanceId { get; set; }

        /// <summary>
        /// Type of operation
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Duration of log retention
        /// </summary>
        public string TimeToDelete { get; set; }

        /// <summary>
        /// The Ip adress of the calling party
        /// </summary>
        public string IpAdress { get; set; }

        /// <summary>
        /// The whole context request
        /// </summary>
        public ContextRequest ContextRequestJson { get; set; }
    }
}
