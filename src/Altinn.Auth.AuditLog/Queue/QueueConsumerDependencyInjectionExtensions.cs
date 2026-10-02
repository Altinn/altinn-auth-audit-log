using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Health;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using System.ComponentModel.DataAnnotations;

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

        // Resolved from a service scope created per batch, so processors (and what they depend on) may be scoped.
        builder.Services.TryAddScoped<IQueueEventProcessor<AuthorizationEvent>, AuthorizationQueueEventProcessor>();
        builder.Services.TryAddScoped<IQueueEventProcessor<AuthenticationEvent>, AuthenticationQueueEventProcessor>();

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
                sp.GetRequiredService<IServiceScopeFactory>(),
                queueSettings,
                all.DepthSampleInterval,
                all.CreateQueuesIfNotExists,
                sp.GetRequiredService<QueueConsumerMetrics>(),
                sp.GetRequiredService<QueueConsumerHealthState>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<QueueBatchConsumer<TEvent>>>());
        });
    }

    /// <summary>
    /// Validates the settings at startup. <c>ValidateDataAnnotations</c> does not descend into the nested
    /// <see cref="QueueSettings"/>, so every enabled queue is validated explicitly here, including the
    /// <c>[Range]</c> attributes and the time spans.
    /// </summary>
    internal static bool ValidateSettings(QueueConsumerSettings settings)
        => ValidateSettings(settings, out _);

    internal static bool ValidateSettings(QueueConsumerSettings settings, out string? error)
    {
        error = null;
        if (!settings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrEmpty(settings.ConnectionString) && string.IsNullOrEmpty(settings.ServiceUri))
        {
            error = "Either ConnectionString or ServiceUri must be set";
            return false;
        }

        if (string.IsNullOrEmpty(settings.ConnectionString) && !Uri.TryCreate(settings.ServiceUri, UriKind.Absolute, out _))
        {
            error = "ServiceUri must be an absolute URI";
            return false;
        }

        if (settings.HealthStaleAfter <= TimeSpan.Zero)
        {
            error = "HealthStaleAfter must be positive";
            return false;
        }

        return ValidateQueue("Authorization", settings.Authorization, out error)
            && ValidateQueue("Authentication", settings.Authentication, out error);
    }

    private static bool ValidateQueue(string name, QueueSettings queue, out string? error)
    {
        error = null;
        if (!queue.Enabled)
        {
            return true;
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(queue, new ValidationContext(queue), results, validateAllProperties: true))
        {
            error = $"{name}: {string.Join("; ", results.Select(r => r.ErrorMessage))}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(queue.QueueName))
        {
            error = $"{name}: QueueName is required";
            return false;
        }

        foreach (var (property, value) in new[]
        {
            (nameof(QueueSettings.VisibilityTimeout), queue.VisibilityTimeout),
            (nameof(QueueSettings.EmptyQueueBackoff), queue.EmptyQueueBackoff),
            (nameof(QueueSettings.ReceiveFailureBackoff), queue.ReceiveFailureBackoff),
            (nameof(QueueSettings.CircuitBreakOpenDuration), queue.CircuitBreakOpenDuration),
            (nameof(QueueSettings.ShutdownGracePeriod), queue.ShutdownGracePeriod),
        })
        {
            if (value <= TimeSpan.Zero)
            {
                error = $"{name}: {property} must be positive";
                return false;
            }
        }

        if (queue.TransientRetryBaseDelay < TimeSpan.Zero)
        {
            error = $"{name}: TransientRetryBaseDelay must not be negative";
            return false;
        }

        if (queue.EmptyQueueMaxBackoff < queue.EmptyQueueBackoff)
        {
            error = $"{name}: EmptyQueueMaxBackoff must be at least EmptyQueueBackoff";
            return false;
        }

        if (queue.ReceiveFailureMaxBackoff < queue.ReceiveFailureBackoff)
        {
            error = $"{name}: ReceiveFailureMaxBackoff must be at least ReceiveFailureBackoff";
            return false;
        }

        // Azure caps the visibility timeout at 7 days.
        if (queue.VisibilityTimeout > TimeSpan.FromDays(7))
        {
            error = $"{name}: VisibilityTimeout must be at most 7 days";
            return false;
        }

        return true;
    }
}
