using Altinn.Auth.AuditLog.Persistence.Interfaces;
using Npgsql;

namespace Altinn.Auth.AuditLog.Persistence.DataSources
{
    public class AppDataSource : IAppDataSource
    {
        private readonly NpgsqlDataSource _dataSource;

        public AppDataSource(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public NpgsqlDataSource GetDataSource() => _dataSource;
    }
}
