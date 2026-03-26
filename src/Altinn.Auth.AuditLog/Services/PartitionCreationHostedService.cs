using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;

namespace Altinn.Auth.AuditLog.Services
{
    public class PartitionCreationHostedService : IHostedService, IDisposable
    {
        private readonly object _lock = new();

        private readonly ILogger<PartitionCreationHostedService> _logger;
        private readonly IPartitionManagerRepository _partitionManagerRepository;
        private readonly TimeProvider _timeProvider;
        private ITimer? _timer;
        private CancellationTokenSource? _stoppingCts;
        private bool _disposed;
        private readonly PartitionCleanupOptions _cleanupOptions;

        private Task _runningJob = Task.CompletedTask;

        /// <summary>
        /// For testing purposes.
        /// </summary>
        internal Task RunningJob => Volatile.Read(ref _runningJob);


        public PartitionCreationHostedService(
            ILogger<PartitionCreationHostedService> logger,
            IPartitionManagerRepository partitionManagerRepository,
            TimeProvider timeProvider,
            IOptions<PartitionCleanupOptions> cleanupOptions)
        {
            _logger = logger;
            _partitionManagerRepository = partitionManagerRepository;
            _timeProvider = timeProvider;
            _cleanupOptions = cleanupOptions.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create linked token to allow cancelling executing task from provided token
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _timer = _timeProvider.CreateTimer(
                static (state) =>
                {
                    var self = (PartitionCreationHostedService)state!;
                    self.ManagePartitionsFromTimer();
                },
                dueTime: TimeSpan.FromDays(1),
                period: TimeSpan.FromDays(1),
                state: this
            );

            // if it does not run at once
            return ManagePartitions(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_disposed) return;
            }

            if (_timer is { } timer)
            {
                await timer.DisposeAsync();
            }

            if (_stoppingCts is { } cts)
            {
                await cts.CancelAsync();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _timer?.Dispose();
            _stoppingCts?.Dispose();
        }

        private void ManagePartitionsFromTimer()
        {
            Task newJob = null!;
            newJob = Task.Run(async () =>
            {
                var token = _stoppingCts!.Token;

                try
                {
                    await ManagePartitions(token);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while creating partitions {ex}");
                }

                lock (_lock)
                {
                    if (Volatile.Read(ref _runningJob) == newJob)
                    {
                        Volatile.Write(ref _runningJob, Task.CompletedTask);
                    }
                }
            });

            lock (_lock)
            {
                var runningJob = Volatile.Read(ref _runningJob);
                if (!runningJob.IsCompleted)
                {
                    newJob = Task.WhenAll(newJob, runningJob);
                }

                Volatile.Write(ref _runningJob, newJob);
            }
        }

        private async Task ManagePartitions(CancellationToken cancellationToken)
        {
            var partitions = GetPartitionsForCurrentAndAdjacentMonths();

            await _partitionManagerRepository.CreatePartitions(partitions, cancellationToken);

            if (_cleanupOptions.EnableOldPartitionDeletion)
            {
                await DeleteOldPartitions(cancellationToken);
            }
        }

        private async Task DeleteOldPartitions(CancellationToken cancellationToken)
        {
            var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            var cutoffDate = new DateOnly(now.Year, now.Month, 1).AddMonths(-_cleanupOptions.RetentionMonths);

            var partitionsToDelete = GetPartitionsToDelete(cutoffDate).ToList();
            if (partitionsToDelete.Any())
            {
                await _partitionManagerRepository.DeletePartitions(partitionsToDelete, cancellationToken);
            }
        }

        internal IEnumerable<Partition> GetPartitionsToDelete(DateOnly cutoffDate)
        {
            string[] schemas = { "authentication", "authz" };
            var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

            // Let's assume you want to check up to 24 months back
            for (int i = 0; i < 24; i++)
            {
                var date = now.AddMonths(-i);
                var (start, end) = GetMonthStartAndEndDate(date);
                if (end <= cutoffDate)
                {
                    var partitionName = $"eventlogv1_y{start.Year}m{start.Month:D2}";
                    foreach (var schema in schemas)
                    {
                        yield return new Partition
                        {
                            SchemaName = schema,
                            Name = partitionName,
                            StartDate = start,
                            EndDate = end
                        };
                    }
                }
            }
        }

        internal IReadOnlyList<Partition> GetPartitionsForCurrentAndAdjacentMonths()
        {
            string authenticationSchemaName = "authentication";
            string authzSchemaName = "authz";

            // Get current dateonly
            var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

            // Define the date ranges for the past, current and next month partitions
            var (currentMonthStartDate, currentMonthEndDate) = GetMonthStartAndEndDate(now);
            var (pastMonthStartDate, pastMonthEndDate) = GetMonthStartAndEndDate(now.AddMonths(-1));
            var (nextMonthStartDate, nextMonthEndDate) = GetMonthStartAndEndDate(now.AddMonths(1));

            // Create partition names
            var pastMonthPartitionName = $"eventlogv1_y{pastMonthStartDate.Year}m{pastMonthStartDate.Month:D2}";
            var currentMonthPartitionName = $"eventlogv1_y{currentMonthStartDate.Year}m{currentMonthStartDate.Month:D2}";
            var nextMonthPartitionName = $"eventlogv1_y{nextMonthStartDate.Year}m{nextMonthStartDate.Month:D2}";

            // List of partitions for both schemas
            return new List<Partition>
            {
                new Partition { SchemaName = authenticationSchemaName, Name = pastMonthPartitionName, StartDate = pastMonthStartDate, EndDate = pastMonthEndDate },
                new Partition { SchemaName = authenticationSchemaName, Name = currentMonthPartitionName, StartDate = currentMonthStartDate, EndDate = currentMonthEndDate },
                new Partition { SchemaName = authenticationSchemaName, Name = nextMonthPartitionName, StartDate = nextMonthStartDate, EndDate = nextMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = pastMonthPartitionName, StartDate = pastMonthStartDate, EndDate = pastMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = currentMonthPartitionName, StartDate = currentMonthStartDate, EndDate = currentMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = nextMonthPartitionName, StartDate = nextMonthStartDate, EndDate = nextMonthEndDate }
            };
        }

        internal (DateOnly startDate, DateOnly endDate) GetMonthStartAndEndDate(DateOnly date)
        {
            DateOnly startDate = new DateOnly(date.Year, date.Month, 1);
            DateOnly endDate = startDate.AddMonths(1);
            return (startDate, endDate);
        }
    }
}
