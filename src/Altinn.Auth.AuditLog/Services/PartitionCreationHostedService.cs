

using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
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

        private Task _runningJob = Task.CompletedTask;

        /// <summary>
        /// For testing purposes.
        /// </summary>
        internal Task RunningJob => Volatile.Read(ref _runningJob);


        public PartitionCreationHostedService(
            ILogger<PartitionCreationHostedService> logger,
            IPartitionManagerRepository partitionManagerRepository,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _partitionManagerRepository = partitionManagerRepository;
            _timeProvider = timeProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create linked token to allow cancelling executing task from provided token
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _timer = _timeProvider.CreateTimer(
                static (state) =>
                {
                    var self = (PartitionCreationHostedService)state!;
                    self.CreateMonthlyPartitionFromTimer();
                },
                dueTime: TimeSpan.FromDays(1), // if this runs at once, change to 1 day
                period: TimeSpan.FromDays(1),
                state: this
            );

            // if it does not run at once
            return CreateMonthlyPartition(cancellationToken);
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

        private void CreateMonthlyPartitionFromTimer()
        {
            Task newJob = null!;
            newJob = Task.Run(async () =>
            {
                var token = _stoppingCts!.Token;
                
                try
                {
                    await CreateMonthlyPartition(token);
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

        private async Task CreateMonthlyPartition(CancellationToken cancellationToken)
        {
            string authenticationSchemaName = "authentication";
            string authzSchemaName = "authz";

            // Get current dateonly
            var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);


            // Define the date ranges for the past, current and next month partitions
            var currentMonthStartDate = now;
            var currentMonthEndDate = currentMonthStartDate.AddMonths(1);
            DateOnly pastMonthStartDate = now.AddMonths(-1);
            DateOnly pastMonthEndDate = now;
            DateOnly nextMonthStartDate = now.AddMonths(1);
            DateOnly nextMonthEndDate = nextMonthStartDate.AddMonths(1);

            // Create partition names
            var pastMonthPartitionName = $"eventlogv1_y{pastMonthStartDate.Year}m{pastMonthStartDate.Month:D2}";
            var currentMonthPartitionName = $"eventlogv1_y{currentMonthStartDate.Year}m{currentMonthStartDate.Month:D2}";
            var nextMonthPartitionName = $"eventlogv1_y{nextMonthStartDate.Year}m{nextMonthStartDate.Month:D2}";

            // List of partitions for both schemas
            IReadOnlyList<Partition> partitions = [
                new Partition { SchemaName = authenticationSchemaName, Name = pastMonthPartitionName, StartDate = pastMonthStartDate, EndDate = pastMonthEndDate },
                new Partition { SchemaName = authenticationSchemaName, Name = currentMonthPartitionName, StartDate = currentMonthStartDate, EndDate = currentMonthEndDate },
                new Partition { SchemaName = authenticationSchemaName, Name = nextMonthPartitionName, StartDate = nextMonthStartDate, EndDate = nextMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = pastMonthPartitionName, StartDate = pastMonthStartDate, EndDate = pastMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = currentMonthPartitionName, StartDate = currentMonthStartDate, EndDate = currentMonthEndDate },
                new Partition { SchemaName = authzSchemaName, Name = nextMonthPartitionName, StartDate = nextMonthStartDate, EndDate = nextMonthEndDate }
            ];

            await _partitionManagerRepository.CreatePartitions(partitions, cancellationToken);
        }
    }
}
