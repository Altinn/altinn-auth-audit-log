using Altinn.Auth.AuditLog.Queue;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using System.Net.Sockets;

namespace Altinn.Auth.AuditLog.Tests.Queue;

public class TransientErrorClassifierTests
{
    [Theory]
    [InlineData("08006")] // connection failure
    [InlineData("53300")] // too many connections
    [InlineData("57P01")] // admin shutdown
    [InlineData("40001")] // serialization failure
    [InlineData("40P01")] // deadlock
    [InlineData("55P03")] // lock not available
    public void PostgresException_InfrastructureClasses_AreTransient(string sqlState)
    {
        var exception = new PostgresException("boom", "ERROR", "ERROR", sqlState);

        Assert.True(TransientErrorClassifier.IsTransient(exception));
    }

    [Theory]
    [InlineData("23503")] // foreign key violation
    [InlineData("22P02")] // invalid text representation
    [InlineData("42703")] // undefined column
    [InlineData("23502")] // not null violation
    public void PostgresException_DataClasses_ArePermanent(string sqlState)
    {
        var exception = new PostgresException("boom", "ERROR", "ERROR", sqlState);

        Assert.False(TransientErrorClassifier.IsTransient(exception));
    }

    [Fact]
    public void PostgresException_MissingPartition_IsTransient()
    {
        var exception = new PostgresException("no partition of relation \"eventlogv1\" found for row", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);

        Assert.True(TransientErrorClassifier.IsTransient(exception));
    }

    [Fact]
    public void PostgresException_OtherCheckViolation_IsPermanent()
    {
        var exception = new PostgresException("new row violates check constraint", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);

        Assert.False(TransientErrorClassifier.IsTransient(exception));
    }

    [Fact]
    public void TimeoutAndIo_AreTransient()
    {
        Assert.True(TransientErrorClassifier.IsTransient(new TimeoutException()));
        Assert.True(TransientErrorClassifier.IsTransient(new IOException()));
        Assert.True(TransientErrorClassifier.IsTransient(new SocketException()));
        Assert.True(TransientErrorClassifier.IsTransient(new NpgsqlException("wrapped", new IOException())));
    }

    [Fact]
    public void GenericExceptions_ArePermanent()
    {
        Assert.False(TransientErrorClassifier.IsTransient(new InvalidOperationException()));
        Assert.False(TransientErrorClassifier.IsTransient(new ArgumentNullException()));
    }
}

public class CircuitBreakerTests
{
    [Fact]
    public void OpensAfterThreshold_AndClosesAfterDuration()
    {
        var time = new FakeTimeProvider();
        var sut = new CircuitBreaker(failuresBeforeOpen: 3, openDuration: TimeSpan.FromSeconds(30), time);

        Assert.False(sut.RecordFailure());
        Assert.False(sut.RecordFailure());
        Assert.False(sut.IsOpen(out _));

        Assert.True(sut.RecordFailure());
        Assert.True(sut.IsOpen(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(30), remaining);

        time.Advance(TimeSpan.FromSeconds(31));
        Assert.False(sut.IsOpen(out _));
    }

    [Fact]
    public void SuccessResetsCounter()
    {
        var time = new FakeTimeProvider();
        var sut = new CircuitBreaker(failuresBeforeOpen: 2, openDuration: TimeSpan.FromSeconds(30), time);

        sut.RecordFailure();
        sut.RecordSuccess();
        Assert.Equal(0, sut.ConsecutiveFailures);

        Assert.False(sut.RecordFailure());
        Assert.False(sut.IsOpen(out _));
    }

    [Fact]
    public void SuccessClosesOpenCircuit()
    {
        var time = new FakeTimeProvider();
        var sut = new CircuitBreaker(failuresBeforeOpen: 1, openDuration: TimeSpan.FromMinutes(5), time);

        sut.RecordFailure();
        Assert.True(sut.IsOpen(out _));

        sut.RecordSuccess();
        Assert.False(sut.IsOpen(out _));
    }
}
