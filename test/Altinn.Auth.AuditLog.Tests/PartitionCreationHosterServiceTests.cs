using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Altinn.Auth.AuditLog.Tests;
public class PartitionCreationHostedServiceTests
{
    private static IOptions<PartitionCleanupOptions> CreateDefaultOptions()
    => Options.Create(new PartitionCleanupOptions { EnableOldPartitionDeletion = false, RetentionMonths = 1 });

    private static IOptions<PartitionCleanupOptions> CreateOptions(bool enable, int retentionMonths)
    => Options.Create(new PartitionCleanupOptions { EnableOldPartitionDeletion = enable, RetentionMonths = retentionMonths });

    [Fact]
    public void GetPartitionsForCurrentAndAdjacentMonths_ReturnsCorrectPartitions()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTime(2023, 10, 15));
        var service = new PartitionCreationHostedService(null, null, timeProvider, CreateDefaultOptions());

        // Act
        var partitions = service.GetPartitionsForCurrentAndAdjacentMonths();

        // Assert
        Assert.Equal(6, partitions.Count);
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2023m09" && p.StartDate == new DateOnly(2023, 9, 1) && p.EndDate == new DateOnly(2023, 10, 1));
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2023m10" && p.StartDate == new DateOnly(2023, 10, 1) && p.EndDate == new DateOnly(2023, 11, 1));
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2023m11" && p.StartDate == new DateOnly(2023, 11, 1) && p.EndDate == new DateOnly(2023, 12, 1));
    }

    [Theory]
    [InlineData(2023, 10, 15, 2023, 10, 1, 2023, 11, 1)]
    [InlineData(2023, 2, 1, 2023, 2, 1, 2023, 3, 1)]
    [InlineData(2024, 2, 1, 2024, 2, 1, 2024, 3, 1)]
    public void GetMonthStartAndEndDate_ReturnsCorrectDates(int year, int month, int day, int expectedStartYear, int expectedStartMonth, int expectedStartDay, int expectedEndYear, int expectedEndMonth, int expectedEndDay)
    {
        // Arrange
        var service = new PartitionCreationHostedService(null, null, null, CreateDefaultOptions());
        var date = new DateOnly(year, month, day);

        // Act
        var (startDate, endDate) = service.GetMonthStartAndEndDate(date);

        // Assert
        Assert.Equal(new DateOnly(expectedStartYear, expectedStartMonth, expectedStartDay), startDate);
        Assert.Equal(new DateOnly(expectedEndYear, expectedEndMonth, expectedEndDay), endDate);
    }

    [Fact]
    public void GetPartitionsForCurrentAndAdjacentMonths_CrossYearBoundary_ReturnsCorrectPartitions()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTime(2023, 12, 15));
        var service = new PartitionCreationHostedService(null, null, timeProvider, CreateDefaultOptions());

        // Act
        var partitions = service.GetPartitionsForCurrentAndAdjacentMonths();

        // Assert
        Assert.Equal(6, partitions.Count);
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2023m11" && p.StartDate == new DateOnly(2023, 11, 1) && p.EndDate == new DateOnly(2023, 12, 1));
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2023m12" && p.StartDate == new DateOnly(2023, 12, 1) && p.EndDate == new DateOnly(2024, 1, 1));
        Assert.Contains(partitions, p => p.Name == "eventlogv1_y2024m01" && p.StartDate == new DateOnly(2024, 1, 1) && p.EndDate == new DateOnly(2024, 2, 1));
    }

    [Fact]
    public async Task StartAsync_CreatesAndDeletes_WhenDeletionEnabled()
    {
        // Arrange
        var now = new DateTime(2023, 12, 15);
        var timeProvider = new FakeTimeProvider(now);
        var mockRepo = new Mock<IPartitionManagerRepository>();
        var options = CreateOptions(true, 1); // Enable deletion, 1 month retention

        var service = new PartitionCreationHostedService(
            null, mockRepo.Object, timeProvider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        mockRepo.Verify(r => r.CreatePartitions(It.IsAny<IReadOnlyList<Partition>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.DeletePartitions(It.Is<IReadOnlyList<Partition>>(partitions =>
            partitions.All(p => p.EndDate < new DateOnly(2023, 11, 15))
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_CreatesButDoesNotDelete_WhenDeletionDisabled()
    {
        // Arrange
        var now = new DateTime(2023, 12, 15);
        var timeProvider = new FakeTimeProvider(now);
        var mockRepo = new Mock<IPartitionManagerRepository>();
        var options = CreateOptions(false, 1); // Deletion disabled

        var service = new PartitionCreationHostedService(
            null, mockRepo.Object, timeProvider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        mockRepo.Verify(r => r.CreatePartitions(It.IsAny<IReadOnlyList<Partition>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.DeletePartitions(It.IsAny<IReadOnlyList<Partition>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2026, 4, 15, 1, "2026-03-01")] // Retain 1 month, cutoff is March 1, 2026
    [InlineData(2026, 4, 15, 2, "2026-02-01")] // Retain 2 months, cutoff is Feb 1, 2026
    [InlineData(2026, 4, 15, 3, "2026-01-01")] // Retain 3 months, cutoff is Jan 1, 2026
    [InlineData(2026, 4, 15, 0, "2026-04-01")] // Retain 0 months, cutoff is April 1, 2026
    public async Task StartAsync_DeletesExpectedPartitions_ForDifferentRetentionMonths(
        int year, int month, int day, int retentionMonths, string expectedCutoff)
    {
        // Arrange
        var now = new DateTime(year, month, day);
        var timeProvider = new FakeTimeProvider(now);
        var mockRepo = new Mock<IPartitionManagerRepository>();
        var options = CreateOptions(true, retentionMonths);

        var service = new PartitionCreationHostedService(
            null, mockRepo.Object, timeProvider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var expectedCutoffDate = DateOnly.Parse(expectedCutoff);
        mockRepo.Verify(r => r.DeletePartitions(It.Is<IReadOnlyList<Partition>>(partitions =>
            partitions.All(p => p.EndDate <= expectedCutoffDate)
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_DoesNotDelete_WhenDeletionDisabled()
    {
        // Arrange
        var now = new DateTime(2026, 4, 15);
        var timeProvider = new FakeTimeProvider(now);
        var mockRepo = new Mock<IPartitionManagerRepository>();
        var options = CreateOptions(false, 1);

        var service = new PartitionCreationHostedService(
            null, mockRepo.Object, timeProvider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        mockRepo.Verify(r => r.DeletePartitions(It.IsAny<IReadOnlyList<Partition>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetPartitionsToDelete_ReturnsExpectedPartitions_ForRetentionMonths()
    {
        // Arrange
        var now = new DateTime(2026, 4, 15);
        var timeProvider = new FakeTimeProvider(now);
        var options = CreateOptions(true, 2);
        var service = new PartitionCreationHostedService(
            null, null, timeProvider, options);

        var cutoffDate = new DateOnly(2026, 2, 1); // For RetentionMonths = 2

        // Act
        var partitionsToDelete = service.GetPartitionsToDelete(cutoffDate).ToList();

        // Assert
        Assert.All(partitionsToDelete, p => Assert.True(p.EndDate <= cutoffDate));
        Assert.True(partitionsToDelete.Count > 0);
    }
}
