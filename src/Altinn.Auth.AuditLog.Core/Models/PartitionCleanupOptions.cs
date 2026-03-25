namespace Altinn.Auth.AuditLog.Core.Models
{
    public class PartitionCleanupOptions
    {
        public bool EnableOldPartitionDeletion { get; set; }
        public int RetentionMonths { get; set; } = 1; // Default: delete partitions older than 1 month
    }
}
