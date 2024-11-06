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
    public class Partition
    {
        public required string Name { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set;}
        public required string SchemaName { get; set; }
    }
}
