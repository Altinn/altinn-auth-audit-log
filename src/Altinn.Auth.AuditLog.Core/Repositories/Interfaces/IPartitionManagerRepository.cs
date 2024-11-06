using Altinn.Auth.AuditLog.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Repositories.Interfaces
{
    /// <summary>
    /// Interface for PostgresSQL operations on partition management
    /// </summary>
    public interface IPartitionManagerRepository
    {
        /// <summary>
        /// Checks and creates necessary partition for authentication event table
        /// </summary>
        /// <param name="tableSchema">the name of the database schema</param>
        /// <param name="partitionName">the name of the table partition to be created</param>
        /// <param name="startDate">starting range for the table partition</param>
        /// <param name="endDate">ending range for the partition</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>true if the partition is created</returns>
        Task<bool> CreatePartition(string tableSchema, string partitionName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks and creates necessary partition for authentication event table
        /// </summary>
        /// <param name="partitions">the list of partitions to be created</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>true if the partition is created</returns>
        Task<bool> CreatePartitions(IReadOnlyList<Partition> partitions, CancellationToken cancellationToken = default);
    }
}
