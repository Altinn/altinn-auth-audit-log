using Altinn.Auth.AuditLog.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Altinn.Auth.AuditLog.Tests.Queue;

/// <summary>
/// Proves the queue metrics reach an OpenTelemetry exporter through the same registration the app uses
/// (<see cref="QueueConsumerDependencyInjectionExtensions.AddQueueConsumers"/>), so a configured
/// Application Insights / OTLP exporter in production will receive them.
/// </summary>
public class QueueConsumerMetricsExportTests
{
    [Fact]
    public void QueueMetrics_AreExported_ThroughTheOpenTelemetryPipeline()
    {
        var exported = new List<Metric>();

        // Other test classes create meters with the same name in parallel and the listener sees them all,
        // so a unique queue tag isolates this test's measurements.
        var queue = "export-" + Guid.NewGuid().ToString("N");

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([new("QueueConsumer:Enabled", "false")]);
        builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddInMemoryExporter(exported));
        builder.AddQueueConsumers();

        using var host = builder.Build();

        // The provider must exist (listening) before measurements are recorded, which is what the OpenTelemetry
        // hosted service guarantees in the app: it starts before the queue consumers.
        var provider = host.Services.GetRequiredService<MeterProvider>();
        var metrics = host.Services.GetRequiredService<QueueConsumerMetrics>();

        metrics.MessagesReceived(queue, 3);
        metrics.MessagesPersisted(queue, 2);
        metrics.MessagePoisoned(queue, "invalid_json");
        metrics.BatchCompleted(queue, "committed", TimeSpan.FromMilliseconds(12));
        metrics.RecordDepth(queue, 42);
        metrics.RecordOldestMessageAge(queue, TimeSpan.FromSeconds(7));
        metrics.CircuitOpened(queue);

        Assert.True(provider.ForceFlush(timeoutMilliseconds: 5_000));

        var byName = exported.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.Last());
        string[] expected =
        [
            "auditlog.queue.messages.received",
            "auditlog.queue.messages.persisted",
            "auditlog.queue.messages.poisoned",
            "auditlog.queue.batches",
            "auditlog.queue.batch.size",
            "auditlog.queue.batch.duration",
            "auditlog.queue.depth",
            "auditlog.queue.oldest_message_age",
            "auditlog.queue.circuit_opened",
        ];

        foreach (var name in expected)
        {
            Assert.True(byName.ContainsKey(name), $"metric '{name}' was not exported; got: {string.Join(", ", byName.Keys)}");
            Assert.Equal(QueueConsumerMetrics.MeterName, byName[name].MeterName);
        }

        Assert.Equal(3, SumLong(byName["auditlog.queue.messages.received"], ("queue", queue)));
        Assert.Equal(1, SumLong(byName["auditlog.queue.messages.poisoned"], ("queue", queue), ("reason", "invalid_json")));
        Assert.Equal(1, SumLong(byName["auditlog.queue.batches"], ("queue", queue), ("outcome", "committed")));
        Assert.Equal(42, GaugeLong(byName["auditlog.queue.depth"], ("queue", queue)));
        Assert.Equal(MetricType.Histogram, byName["auditlog.queue.batch.duration"].MetricType);
        Assert.Equal("ms", byName["auditlog.queue.batch.duration"].Unit);
    }

    private static long SumLong(Metric metric, params (string Key, string Value)[] tags)
    {
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            if (HasTags(in point, tags))
            {
                return point.GetSumLong();
            }
        }

        throw new Xunit.Sdk.XunitException($"no metric point on '{metric.Name}' with tags {string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    private static long GaugeLong(Metric metric, params (string Key, string Value)[] tags)
    {
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            if (HasTags(in point, tags))
            {
                return point.GetGaugeLastValueLong();
            }
        }

        throw new Xunit.Sdk.XunitException($"no metric point on '{metric.Name}' with tags {string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    private static bool HasTags(in MetricPoint point, (string Key, string Value)[] tags)
    {
        var found = 0;
        foreach (var tag in point.Tags)
        {
            if (tags.Any(t => t.Key == tag.Key && t.Value == tag.Value?.ToString()))
            {
                found++;
            }
        }

        return found == tags.Length;
    }
}
