using Altinn.Auth.AuditLog.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Repositories.Interfaces
{
    /// <summary>
    /// Interface for PostgresSQL operations on authentication event
    /// </summary>
    public interface IAuthenticationEventRepository
    {
        /// <summary>
        /// inserts an authentication event to the database
        /// </summary>
        /// <param name="authenticationEvent"></param>
        /// <returns></returns>
        Task InsertAuthenticationEvent(AuthenticationEvent authenticationEvent);

        /// <summary>
        /// Checks and creates necessary partition for authentication event table
        /// </summary>
        /// <param name="partitionName">the name of the table partition to be created</param>
        /// <param name="startDate">starting range for the table partition</param>
        /// <param name="endDate">ending range for the partition</param>
        /// <returns>true if the partition is created</returns>
        Task<bool> CreatePartition(string partitionName, DateTime startDate, DateTime endDate);
    }
}
