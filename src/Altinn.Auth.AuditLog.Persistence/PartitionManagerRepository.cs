using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Yuniql.Core;

namespace Altinn.Auth.AuditLog.Persistence
{
    [ExcludeFromCodeCoverage]
    public class PartitionManagerRepository : IPartitionManagerRepository
    {
        private readonly ILogger _logger;
        private readonly NpgsqlDataSource _dataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionManagerRepository"/> class
        /// </summary>
        /// <param name="dataSource">The postgreSQL datasource for AuditLogDB</param>
        /// <param name="logger">handler for logger service</param>
        public PartitionManagerRepository(
            [FromKeyedServices(typeof(IPartitionManagerRepository))] NpgsqlDataSource adminDataSource,
            ILogger<AuthenticationEventRepository> logger)
        {
            _dataSource = adminDataSource;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> CreatePartition(string tableSchema, string partitionName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            string tableName = "eventlogv1";
            try
            {
                string CREATEPARTITION =
                    /*strpsql*/$"""
                    CREATE TABLE IF NOT EXISTS {tableSchema}.{partitionName} PARTITION OF {tableSchema}.{tableName}
                    FOR VALUES FROM ('{startDate:yyyy-MM-dd}') TO ('{endDate:yyyy-MM-dd}')
                    """;
                await using NpgsqlCommand pgcom = _dataSource.CreateCommand(CREATEPARTITION);

                var result = await pgcom.ExecuteNonQueryAsync(cancellationToken);
                if (result == 0)
                {
                    _logger.LogInformation($"Partition already exists: {partitionName}");
                    return false;
                }
                else
                {
                    _logger.LogInformation($"Created partition: {partitionName}");
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "AuditLog // PartitionManagerRepository // CreatePartition // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CreatePartitions(IReadOnlyList<Partition> partitions, CancellationToken cancellationToken = default)
        {
            // Start a batch to execute multiple statements on the same connection
            await using (var batch = _dataSource.CreateBatch())
            {
                try
                {
                    // Iterate over the list of partitions and create each one
                    foreach (var partition in partitions)
                    {
                        var cmd = batch.CreateBatchCommand();
                        cmd.CommandText = /*strpsql*/$"""
                            CREATE TABLE IF NOT EXISTS {partition.SchemaName}.{partition.Name}
                            PARTITION OF {partition.SchemaName}.eventlogv1
                            FOR VALUES FROM ('{partition.StartDate:yyyy-MM-dd}') TO ('{partition.EndDate:yyyy-MM-dd}')
                            """;
                        batch.BatchCommands.Add(cmd);
                    }

                    await batch.ExecuteNonQueryAsync(cancellationToken);
                    return true;

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AuditLog // PartitionManagerRepository // CreatePartition // Exception");
                    throw;
                }
            }
        }
    }
}
