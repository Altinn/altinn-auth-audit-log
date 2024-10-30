

using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Npgsql;

namespace Altinn.Auth.AuditLog.Services
{
    public class PartitionCreationService :  BackgroundService
    {
        private readonly ILogger<PartitionCreationService> _logger;
        private readonly IAuthenticationEventRepository _authEventRepository;

        public PartitionCreationService(ILogger<PartitionCreationService> logger,
            IAuthenticationEventRepository authEventRepository)
        {
            _logger = logger;
            _authEventRepository = authEventRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Partition Creation Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await CreateMonthlyPartition(stoppingToken);
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }

        private async Task CreateMonthlyPartition(CancellationToken stoppingToken)
        {
            // Get current month and next month
            var now = DateTime.UtcNow;
            var currentYear = now.Year;
            var currentMonth = now.Month;
            var nextMonth = now.AddMonths(1);

            // Create partition names
            var currentMonthPartitionName = $"eventlogv1_y{currentYear}m{currentMonth:D2}";
            var nextMonthPartitionName = $"eventlogv1_y{nextMonth.Year}m{nextMonth.Month:D2}";

            // Define the date ranges for the current and next month partitions
            var currentMonthStartDate = new DateTime(currentYear, currentMonth, 1);
            var currentMonthEndDate = currentMonthStartDate.AddMonths(1);

            DateTime nextMonthStartDate = new DateTime(nextMonth.Year, nextMonth.Month, 1);
            DateTime nextMonthEndDate = nextMonthStartDate.AddMonths(1);

            await _authEventRepository.CreatePartition(currentMonthPartitionName, currentMonthStartDate, currentMonthEndDate);

            await _authEventRepository.CreatePartition(nextMonthPartitionName, nextMonthStartDate, nextMonthEndDate);
        }
    }
}
