using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Models
{
    /// <summary>
    /// Used for partition creation
    /// </summary>
    public sealed record Partition
    {
        public required string Name { get; set; }
        public required DateOnly StartDate { get; set; }
        public required DateOnly EndDate { get; set;}
        public required string SchemaName { get; set; }
    }
}
