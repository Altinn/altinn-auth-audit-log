using Altinn.Auth.AuditLog.Queue;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using System.Net.Sockets;

namespace Altinn.Auth.AuditLog.Tests.Queue;

public class PersistFailureClassifierTests
{
    [Theory]
    [InlineData("08006")] // connection failure
    [InlineData("53300")] // too many connections
    [InlineData("57P01")] // admin shutdown
    [InlineData("40001")] // serialization failure
    [InlineData("40P01")] // deadlock
    [InlineData("55P03")] // lock not available
    [InlineData("XX000")] // internal error
    public void PostgresException_InfrastructureClasses_AreTransient(string sqlState)
    {
        var exception = new PostgresException("boom", "ERROR", "ERROR", sqlState);

        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("23503")] // foreign key violation (unknown decision/eventtype id)
    [InlineData("23502")] // not null violation
    [InlineData("23505")] // unique violation
    [InlineData("22P02")] // invalid text representation
    [InlineData("22003")] // numeric value out of range
    [InlineData("22001")] // string data right truncation
    public void PostgresException_DataClasses_AreDataErrors(string sqlState)
    {
        var exception = new PostgresException("boom", "ERROR", "ERROR", sqlState);

        Assert.Equal(PersistFailureClass.DataError, PersistFailureClassifier.Classify(exception));
    }

    [Theory]
    [InlineData("42703")] // undefined column (schema mismatch)
    [InlineData("42P01")] // undefined table
    [InlineData("42501")] // insufficient privilege
    [InlineData("42601")] // syntax error
    [InlineData("3F000")] // invalid schema name
    [InlineData("28P01")] // invalid password
    [InlineData("0A000")] // feature not supported
    public void PostgresException_SchemaPrivilegeConfig_AreSystemic_NotDataErrors(string sqlState)
    {
        var exception = new PostgresException("boom", "ERROR", "ERROR", sqlState);

        Assert.Equal(PersistFailureClass.Systemic, PersistFailureClassifier.Classify(exception));
    }

    [Fact]
    public void PostgresException_MissingPartition_IsTransient()
    {
        var exception = new PostgresException("no partition of relation \"eventlogv1\" found for row", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);

        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(exception));
    }

    [Fact]
    public void PostgresException_OtherCheckViolation_IsDataError()
    {
        var exception = new PostgresException("new row violates check constraint", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);

        Assert.Equal(PersistFailureClass.DataError, PersistFailureClassifier.Classify(exception));
    }

    [Fact]
    public void TimeoutAndIo_AreTransient()
    {
        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(new TimeoutException()));
        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(new IOException()));
        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(new SocketException()));
        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(new NpgsqlException("wrapped", new IOException())));
        Assert.True(PersistFailureClassifier.IsTransient(new TimeoutException()));
    }

    [Fact]
    public void RepositoryGuards_AreDataErrors()
    {
        Assert.Equal(PersistFailureClass.DataError, PersistFailureClassifier.Classify(new ArgumentNullException("events", "Created must not be null")));
        Assert.Equal(PersistFailureClass.DataError, PersistFailureClassifier.Classify(new ArgumentException("bad")));
    }

    [Fact]
    public void UnknownExceptions_AreSystemic_NeverPoison()
    {
        Assert.Equal(PersistFailureClass.Systemic, PersistFailureClassifier.Classify(new InvalidOperationException()));
        Assert.Equal(PersistFailureClass.Systemic, PersistFailureClassifier.Classify(new NullReferenceException()));
        Assert.Equal(PersistFailureClass.Systemic, PersistFailureClassifier.Classify(new NpgsqlException("no inner")));
    }

    [Fact]
    public void Aggregate_TakesTheWorstClass()
    {
        Assert.Equal(PersistFailureClass.Transient, PersistFailureClassifier.Classify(new AggregateException(new TimeoutException(), new IOException())));
        Assert.Equal(PersistFailureClass.DataError, PersistFailureClassifier.Classify(new AggregateException(new TimeoutException(), new ArgumentException())));
        Assert.Equal(PersistFailureClass.Systemic, PersistFailureClassifier.Classify(new AggregateException(new ArgumentException(), new InvalidOperationException())));
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
