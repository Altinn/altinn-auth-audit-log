using Altinn.Auth.AuditLog.Persistence.Interfaces;
using Npgsql;

namespace Altinn.Auth.AuditLog.Persistence.DataSources
{
    public class AdminDataSource : IAdminDataSource
    {
        private readonly NpgsqlDataSource _dataSource;

        public AdminDataSource(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public NpgsqlDataSource GetDataSource() => _dataSource;
    }
}
