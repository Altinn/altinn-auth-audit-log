using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Models
{
    /// <summary>
    /// General configuration settings
    /// </summary>
    public class KeyVaultSettings
    {
        /// <summary>
        /// Gets or sets the secret uri
        /// </summary>
        public string SecretUri { get; set; }
    }
}
