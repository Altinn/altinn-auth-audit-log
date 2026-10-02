using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Health;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Registers the in-process Storage Queue consumers.
/// </summary>
public static class QueueConsumerDependencyInjectionExtensions
{
    /// <summary>
    /// Registers metrics, health check and (when <c>QueueConsumer:Enabled</c> is <see langword="true"/>) one
    /// <see cref="QueueBatchConsumer{TEvent}"/> per configured queue.
    /// </summary>
    public static IHostApplicationBuilder AddQueueConsumers(this IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(QueueConsumerSettings.SectionName);

        builder.Services.AddOptions<QueueConsumerSettings>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(ValidateSettings, "QueueConsumer settings are invalid: when enabled, either ConnectionString or ServiceUri must be set, and every enabled queue needs a QueueName")
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<QueueConsumerMetrics>();
        builder.Services.TryAddSingleton<QueueConsumerHealthState>();
        builder.Services.ConfigureOpenTelemetryMeterProvider(provider => provider.AddMeter(QueueConsumerMetrics.MeterName));
        builder.TryAddHealthCheck("queue-consumer", healthChecks => healthChecks.AddCheck<QueueConsumerHealthCheck>("queue-consumer"));

        var settings = section.Get<QueueConsumerSettings>();
        if (settings is not { Enabled: true })
        {
            return builder;
        }

        builder.Services.AddAzureClients(clients =>
        {
            var client = !string.IsNullOrEmpty(settings.ConnectionString)
                ? clients.AddQueueServiceClient(settings.ConnectionString)
                : clients.AddQueueServiceClient(new Uri(settings.ServiceUri!));

            // Base64 matches the Azure Functions queue trigger default, so bodies (and poison copies) are byte-for-byte compatible.
            client.ConfigureOptions(options => options.MessageEncoding = QueueMessageEncoding.Base64);
            clients.UseCredential(new DefaultAzureCredential());
        });

        builder.Services.TryAddSingleton<IQueueEventProcessor<AuthorizationEvent>, AuthorizationQueueEventProcessor>();
        builder.Services.TryAddSingleton<IQueueEventProcessor<AuthenticationEvent>, AuthenticationQueueEventProcessor>();

        if (settings.Authorization.Enabled)
        {
            AddConsumer<AuthorizationEvent>(builder.Services, static s => s.Authorization);
        }

        if (settings.Authentication.Enabled)
        {
            AddConsumer<AuthenticationEvent>(builder.Services, static s => s.Authentication);
        }

        return builder;
    }

    private static void AddConsumer<TEvent>(IServiceCollection services, Func<QueueConsumerSettings, QueueSettings> select)
    {
        services.AddSingleton<IHostedService>(sp =>
        {
            var all = sp.GetRequiredService<IOptions<QueueConsumerSettings>>().Value;
            var queueSettings = select(all);
            var serviceClient = sp.GetRequiredService<QueueServiceClient>();
            var queue = new StorageRawQueue(
                serviceClient.GetQueueClient(queueSettings.QueueName),
                serviceClient.GetQueueClient(queueSettings.EffectivePoisonQueueName));

            return new QueueBatchConsumer<TEvent>(
                queue,
                sp.GetRequiredService<IQueueEventProcessor<TEvent>>(),
                queueSettings,
                all.DepthSampleInterval,
                sp.GetRequiredService<QueueConsumerMetrics>(),
                sp.GetRequiredService<QueueConsumerHealthState>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<QueueBatchConsumer<TEvent>>>());
        });
    }

    private static bool ValidateSettings(QueueConsumerSettings settings)
    {
        if (!settings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrEmpty(settings.ConnectionString) && string.IsNullOrEmpty(settings.ServiceUri))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(settings.ServiceUri) && string.IsNullOrEmpty(settings.ConnectionString) && !Uri.TryCreate(settings.ServiceUri, UriKind.Absolute, out _))
        {
            return false;
        }

        return ValidateQueue(settings.Authorization) && ValidateQueue(settings.Authentication);

        static bool ValidateQueue(QueueSettings queue)
            => !queue.Enabled || !string.IsNullOrWhiteSpace(queue.QueueName);
    }
}
