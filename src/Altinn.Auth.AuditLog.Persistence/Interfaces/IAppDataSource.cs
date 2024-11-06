using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Persistence.Interfaces
{
    /// <summary>
    /// Interface for admin data source connection to postgressql
    /// </summary>
    public interface IAppDataSource
    {
        public NpgsqlDataSource GetDataSource();
    }
}
