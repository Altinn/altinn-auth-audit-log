using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Queue;
using Altinn.Auth.AuditLog.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Diagnostics.Metrics;
using System.Text;

namespace Altinn.Auth.AuditLog.Tests.Queue;

public class QueueBatchConsumerTests
{
    private static readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ProcessBatch_AllDecodable_CommitsOnceAndDeletesAll()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);
        var messages = Messages("a", "b", "c");

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(messages, CancellationToken.None);

        Assert.Equal(BatchOutcome.Committed, outcome);
        var batch = Assert.Single(processor.Batches);
        Assert.Equal(["a", "b", "c"], batch);
        Assert.Equal(3, queue.Deleted.Count);
        Assert.Empty(queue.Poisoned);
        Assert.Equal(0, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_ResolvesProcessorFromScopePerBatch()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor();
        var scopes = 0;
        var sut = CreateConsumer(queue, processor, onResolve: () => scopes++);

        await sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), CancellationToken.None);
        await sut.ProcessReceivedMessagesForTestingAsync(Messages("b"), CancellationToken.None);

        Assert.Equal(2, scopes);
        Assert.Equal(2, processor.Batches.Count);
    }

    [Fact]
    public async Task ProcessBatch_UndecodableMessage_IsPoisoned_RestIsCommitted()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);
        var messages = Messages("a", "bad-1", "c");

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(messages, CancellationToken.None);

        Assert.Equal(BatchOutcome.Committed, outcome);
        Assert.Equal(["a", "c"], Assert.Single(processor.Batches));
        var poisoned = Assert.Single(queue.Poisoned);
        Assert.Equal("bad-1", poisoned.Body.ToString());
        Assert.Equal(3, queue.Deleted.Count); // 2 committed + 1 poisoned
    }

    [Fact]
    public async Task ProcessBatch_OnlyUndecodable_NothingPersisted()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("bad-1"), CancellationToken.None);

        Assert.Equal(BatchOutcome.NothingToPersist, outcome);
        Assert.Empty(processor.Batches);
        Assert.Single(queue.Poisoned);
        Assert.Single(queue.Deleted);
    }

    [Fact]
    public async Task ProcessBatch_DequeueCountExceeded_IsPoisonedWithoutDecoding()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor, s => s.MaxDequeueCount = 5);
        var exhausted = Message("looks-fine", dequeueCount: 6);
        var fresh = Message("fresh", dequeueCount: 5);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync([exhausted, fresh], CancellationToken.None);

        Assert.Equal(BatchOutcome.Committed, outcome);
        Assert.Equal(["fresh"], Assert.Single(processor.Batches));
        Assert.Same(exhausted, Assert.Single(queue.Poisoned));
        Assert.DoesNotContain("looks-fine", processor.Decoded);
    }

    [Fact]
    public async Task ProcessBatch_TransientFailure_RetriesThenLeavesMessagesOnQueue()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor { Persist = (_, _) => throw new TimeoutException("db is slow") };
        var sut = CreateConsumer(queue, processor, s => s.TransientRetryAttempts = 3);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a", "b"), CancellationToken.None);

        Assert.Equal(BatchOutcome.TransientFailure, outcome);
        Assert.Equal(3, processor.Batches.Count); // 1 + 2 retries
        Assert.Empty(queue.Deleted);
        Assert.Empty(queue.Poisoned);
        Assert.Equal(1, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_TransientThenSuccess_Commits()
    {
        var queue = new FakeRawQueue();
        var calls = 0;
        var processor = new FakeProcessor
        {
            Persist = (_, _) => ++calls == 1 ? throw new TimeoutException() : Task.CompletedTask,
        };
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), CancellationToken.None);

        Assert.Equal(BatchOutcome.Committed, outcome);
        Assert.Equal(2, calls);
        Assert.Single(queue.Deleted);
        Assert.Equal(0, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_DataErrorOnBatch_FallsBackPerMessage_AndPoisonsOnlyTheBadOne()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor
        {
            Persist = (events, _) => events.Contains("poisonme")
                ? throw DataError()
                : Task.CompletedTask,
        };
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a", "poisonme", "c"), CancellationToken.None);

        Assert.Equal(BatchOutcome.FallbackPerMessage, outcome);
        Assert.Equal(4, processor.Batches.Count); // 1 batch + 3 singles
        Assert.Equal("poisonme", Assert.Single(queue.Poisoned).Body.ToString());
        Assert.Equal(3, queue.Deleted.Count); // 2 committed + 1 poisoned
        Assert.Equal(0, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_FallbackHitsTransient_LeavesThatMessage()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor
        {
            Persist = (events, _) => events.Count > 1
                ? throw DataError()
                : events[0] == "flaky" ? throw new TimeoutException() : Task.CompletedTask,
        };
        var sut = CreateConsumer(queue, processor, s => s.TransientRetryAttempts = 2);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a", "flaky"), CancellationToken.None);

        Assert.Equal(BatchOutcome.FallbackPerMessage, outcome);
        Assert.Equal("a", Assert.Single(queue.Deleted).Body.ToString());
        Assert.Empty(queue.Poisoned);
        Assert.Equal(1, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_RepeatedTransientFailures_OpenCircuit()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor { Persist = (_, _) => throw new TimeoutException() };
        var sut = CreateConsumer(queue, processor, s =>
        {
            s.TransientRetryAttempts = 1;
            s.CircuitBreakFailuresBeforeOpen = 2;
            s.CircuitBreakOpenDuration = TimeSpan.FromMinutes(1);
        });

        await sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), CancellationToken.None);
        Assert.False(sut.Circuit.IsOpen(out _));

        await sut.ProcessReceivedMessagesForTestingAsync(Messages("b"), CancellationToken.None);
        Assert.True(sut.Circuit.IsOpen(out var remaining));
        Assert.True(remaining > TimeSpan.Zero);
    }

    [Theory]
    [InlineData("42703")] // undefined column: schema mismatch
    [InlineData("42501")] // insufficient privilege
    public async Task ProcessBatch_SystemicFailure_PoisonsNothing_LeavesMessages_AndTripsCircuit(string sqlState)
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor
        {
            Persist = (_, _) => throw new PostgresException("boom", "ERROR", "ERROR", sqlState),
        };
        var sut = CreateConsumer(queue, processor, s => s.CircuitBreakFailuresBeforeOpen = 1);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a", "b", "c"), CancellationToken.None);

        Assert.Equal(BatchOutcome.SystemicFailure, outcome);
        Assert.Single(processor.Batches); // no per-message fallback, no in-process retry
        Assert.Empty(queue.Poisoned);
        Assert.Empty(queue.Deleted);
        Assert.True(sut.Circuit.IsOpen(out _));
    }

    [Fact]
    public async Task ProcessBatch_UnknownException_IsTreatedAsSystemic_NotPoisoned()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor { Persist = (_, _) => throw new InvalidOperationException("bug") };
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), CancellationToken.None);

        Assert.Equal(BatchOutcome.SystemicFailure, outcome);
        Assert.Empty(queue.Poisoned);
        Assert.Empty(queue.Deleted);
        Assert.Equal(1, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_FallbackHitsSystemic_LeavesThatMessage_AndTripsCircuit()
    {
        // Batch rejected as a data error, but the per-message insert reveals a system problem for one message.
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor
        {
            Persist = (events, _) => events.Count > 1
                ? throw DataError()
                : events[0] == "sys" ? throw new PostgresException("boom", "ERROR", "ERROR", "42501") : Task.CompletedTask,
        };
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a", "sys"), CancellationToken.None);

        Assert.Equal(BatchOutcome.FallbackPerMessage, outcome);
        Assert.Equal("a", Assert.Single(queue.Deleted).Body.ToString());
        Assert.Empty(queue.Poisoned);
        Assert.Equal(1, sut.Circuit.ConsecutiveFailures);
    }

    [Fact]
    public async Task ProcessBatch_DeleteFails_AfterCommit_DoesNotThrow()
    {
        var queue = new FakeRawQueue { FailDelete = true };
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);

        var outcome = await sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), CancellationToken.None);

        Assert.Equal(BatchOutcome.Committed, outcome);
        Assert.Single(processor.Batches);
        Assert.Empty(queue.Deleted);
    }

    [Fact]
    public async Task ProcessBatch_PoisonSendFails_MessageIsNotDeleted()
    {
        var queue = new FakeRawQueue { FailPoison = true };
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);

        await sut.ProcessReceivedMessagesForTestingAsync(Messages("bad-1"), CancellationToken.None);

        Assert.Empty(queue.Poisoned);
        Assert.Empty(queue.Deleted);
    }

    [Fact]
    public async Task ProcessBatch_Cancelled_Propagates()
    {
        var queue = new FakeRawQueue();
        var processor = new FakeProcessor { Persist = (_, ct) => Task.FromCanceled(ct) };
        var sut = CreateConsumer(queue, processor);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ProcessReceivedMessagesForTestingAsync(Messages("a"), cts.Token));

        Assert.Empty(queue.Deleted);
    }

    [Fact]
    public async Task Execute_DrainsQueueInBatches_Sequentially_AndStopsCleanly()
    {
        var queue = new FakeRawQueue();
        for (var i = 0; i < 70; i++)
        {
            queue.Pending.Enqueue(Message($"m{i}"));
        }

        var processor = new FakeProcessor { Persist = async (_, ct) => await Task.Delay(5, ct) };
        var sut = CreateConsumer(queue, processor, s => s.BatchSize = 32);

        await sut.StartAsync(CancellationToken.None);
        await WaitUntil(() => queue.Deleted.Count == 70);
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(70, queue.Deleted.Count);
        Assert.Equal([32, 32, 6], processor.Batches.Select(b => b.Count));
        Assert.Equal(1, processor.MaxObservedConcurrency); // one batch at a time per consumer
        Assert.True(sut.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Execute_ReceiveFailure_BacksOffAndKeepsRunning()
    {
        var queue = new FakeRawQueue { FailReceiveTimes = 2 };
        queue.Pending.Enqueue(Message("a"));
        var processor = new FakeProcessor();
        var sut = CreateConsumer(queue, processor);

        await sut.StartAsync(CancellationToken.None);
        await WaitUntil(() => queue.Deleted.Count == 1);
        await sut.StopAsync(CancellationToken.None);

        Assert.Single(processor.Batches);
        Assert.Equal(3, queue.ReceiveCalls); // 2 failures + 1 success (further polls are empty)
    }

    [Fact]
    public async Task Execute_Stop_WaitsForInFlightBatch()
    {
        var queue = new FakeRawQueue();
        queue.Pending.Enqueue(Message("slow"));
        var gate = new TaskCompletionSource();
        var processor = new FakeProcessor { Persist = (_, _) => gate.Task };
        var sut = CreateConsumer(queue, processor);

        await sut.StartAsync(CancellationToken.None);
        await WaitUntil(() => processor.Batches.Count == 1);

        var stopping = sut.StopAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(stopping.IsCompleted); // in-flight batch is still running

        gate.SetResult();
        await stopping;

        Assert.Single(queue.Deleted); // the batch completed and deleted its message during shutdown
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(_testTimeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    private static QueueBatchConsumer<string> CreateConsumer(FakeRawQueue queue, FakeProcessor processor, Action<QueueSettings>? configure = null, Action? onResolve = null)
    {
        var settings = new QueueSettings
        {
            QueueName = queue.Name,
            BatchSize = 32,
            MaxDequeueCount = 5,
            TransientRetryAttempts = 3,
            TransientRetryBaseDelay = TimeSpan.FromMilliseconds(1),
            EmptyQueueBackoff = TimeSpan.FromMilliseconds(5),
            EmptyQueueMaxBackoff = TimeSpan.FromMilliseconds(20),
            ReceiveFailureBackoff = TimeSpan.FromMilliseconds(5),
            ReceiveFailureMaxBackoff = TimeSpan.FromMilliseconds(20),
            CircuitBreakFailuresBeforeOpen = 5,
            CircuitBreakOpenDuration = TimeSpan.FromSeconds(1),
            ShutdownGracePeriod = TimeSpan.FromSeconds(5),
        };
        configure?.Invoke(settings);

        var services = new ServiceCollection();
        services.AddScoped<IQueueEventProcessor<string>>(_ =>
        {
            onResolve?.Invoke();
            return processor;
        });

        return new QueueBatchConsumer<string>(
            queue,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            settings,
            depthSampleInterval: TimeSpan.Zero,
            createQueuesIfNotExists: false,
            new QueueConsumerMetrics(new TestMeterFactory()),
            new QueueConsumerHealthState(),
            TimeProvider.System,
            NullLogger<QueueBatchConsumer<string>>.Instance);
    }

    private static RawQueueMessage[] Messages(params string[] bodies)
        => bodies.Select(b => Message(b)).ToArray();

    private static RawQueueMessage Message(string body, long dequeueCount = 1)
        => new(Guid.NewGuid().ToString("N"), "pop-" + Guid.NewGuid().ToString("N"), BinaryData.FromString(body), dequeueCount, DateTimeOffset.UtcNow);

    /// <summary>A foreign key violation: the database rejects the row because of its data.</summary>
    private static PostgresException DataError()
        => new("insert or update violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);

    /// <summary>
    /// A processor over plain strings: bodies starting with "bad" are undecodable; persistence is pluggable.
    /// </summary>
    private sealed class FakeProcessor : IQueueEventProcessor<string>
    {
        private int _inFlight;

        public List<IReadOnlyList<string>> Batches { get; } = [];

        public List<string> Decoded { get; } = [];

        public int MaxObservedConcurrency { get; private set; }

        public Func<IReadOnlyList<string>, CancellationToken, Task> Persist { get; set; } = (_, _) => Task.CompletedTask;

        public string Kind => "test";

        public string Decode(ReadOnlySpan<byte> body)
        {
            var text = Encoding.UTF8.GetString(body);
            if (text.StartsWith("bad", StringComparison.Ordinal))
            {
                throw new MessageDecodeException(MessageDecodeException.Reasons.InvalidJson, "bad message");
            }

            lock (Decoded)
            {
                Decoded.Add(text);
            }

            return text;
        }

        public async Task PersistAsync(IReadOnlyList<string> events, CancellationToken cancellationToken)
        {
            lock (Batches)
            {
                Batches.Add(events.ToArray());
                _inFlight++;
                MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, _inFlight);
            }

            try
            {
                await Persist(events, cancellationToken);
            }
            finally
            {
                lock (Batches)
                {
                    _inFlight--;
                }
            }
        }
    }

    private sealed class FakeRawQueue : IRawQueue
    {
        private readonly Lock _lock = new();

        public string Name => "testqueue";

        public Queue<RawQueueMessage> Pending { get; } = new();

        public List<RawQueueMessage> Deleted { get; } = [];

        public List<RawQueueMessage> Poisoned { get; } = [];

        public bool FailDelete { get; set; }

        public bool FailPoison { get; set; }

        public int FailReceiveTimes { get; set; }

        public int ReceiveCalls { get; private set; }

        public Task EnsureExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> ReceiveAsync(int maxMessages, TimeSpan visibilityTimeout, List<RawQueueMessage> destination, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (Pending.Count > 0 || FailReceiveTimes > 0)
                {
                    ReceiveCalls++;
                }

                if (FailReceiveTimes > 0)
                {
                    FailReceiveTimes--;
                    throw new IOException("storage unavailable");
                }

                var received = 0;
                while (received < maxMessages && Pending.TryDequeue(out var message))
                {
                    destination.Add(message);
                    received++;
                }

                return Task.FromResult(received);
            }
        }

        public Task DeleteAsync(RawQueueMessage message, CancellationToken cancellationToken)
        {
            if (FailDelete)
            {
                throw new IOException("delete failed");
            }

            lock (_lock)
            {
                Deleted.Add(message);
            }

            return Task.CompletedTask;
        }

        public Task SendToPoisonAsync(RawQueueMessage message, CancellationToken cancellationToken)
        {
            if (FailPoison)
            {
                throw new IOException("poison send failed");
            }

            lock (_lock)
            {
                Poisoned.Add(message);
            }

            return Task.CompletedTask;
        }

        public Task<long?> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult<long?>(Pending.Count);
            }
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose()
        {
        }
    }
}
