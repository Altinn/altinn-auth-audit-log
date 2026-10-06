using System.ComponentModel.DataAnnotations;

namespace Altinn.Auth.AuditLog.Configuration;

/// <summary>
/// Settings for the in-process Storage Queue consumers (configuration section <c>QueueConsumer</c>).
/// </summary>
public class QueueConsumerSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "QueueConsumer";

    /// <summary>
    /// Feature flag. When <see langword="false"/> (the default) no consumer is started and the
    /// container app behaves exactly as before (HTTP API only).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Storage account connection string (for instance <c>UseDevelopmentStorage=true</c> for Azurite).
    /// Takes precedence over <see cref="ServiceUri"/> when set.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Queue service URI (<c>https://&lt;account&gt;.queue.core.windows.net</c>), used together with
    /// <c>DefaultAzureCredential</c> (managed identity / workload identity). Requires the roles
    /// <em>Storage Queue Data Message Processor</em> on the source queues and
    /// <em>Storage Queue Data Message Sender</em> on the poison queues.
    /// </summary>
    public string? ServiceUri { get; set; }

    /// <summary>
    /// When <see langword="true"/> the consumer creates the source and poison queues at startup if they do not exist.
    /// This requires <c>Microsoft.Storage/storageAccounts/queueServices/queues/write</c> (e.g. <em>Storage Queue Data
    /// Contributor</em>), which the message-level roles documented on <see cref="ServiceUri"/> do not grant. Defaults to
    /// <see langword="false"/>: queues are expected to be provisioned by infrastructure (and the producers already create
    /// the source queues). Useful for local development against Azurite.
    /// </summary>
    public bool CreateQueuesIfNotExists { get; set; }

    /// <summary>
    /// A consumer that has not completed a successful receive within this period is reported as unhealthy.
    /// </summary>
    public TimeSpan HealthStaleAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the approximate queue depth is sampled for the <c>auditlog.queue.depth</c> metric.
    /// </summary>
    public TimeSpan DepthSampleInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Settings for the <c>authorizationeventlog</c> queue consumer.
    /// </summary>
    public QueueSettings Authorization { get; set; } = new() { QueueName = "authorizationeventlog" };

    /// <summary>
    /// Settings for the <c>eventlog</c> (authentication) queue consumer.
    /// </summary>
    public QueueSettings Authentication { get; set; } = new() { QueueName = "eventlog" };
}

/// <summary>
/// Settings for a single queue consumer.
/// </summary>
public class QueueSettings
{
    /// <summary>
    /// Azure Storage Queue hard limit for a single receive call.
    /// </summary>
    public const int MaxBatchSize = 32;

    /// <summary>
    /// Whether this particular consumer is enabled (requires <see cref="QueueConsumerSettings.Enabled"/> as well).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The name of the queue to consume.
    /// </summary>
    [Required]
    public required string QueueName { get; set; }

    /// <summary>
    /// The name of the poison queue. Defaults to <c>{QueueName}-poison</c>, which is the convention
    /// the Azure Functions runtime used, so existing alerts keep working.
    /// </summary>
    public string? PoisonQueueName { get; set; }

    /// <summary>
    /// Number of messages to receive per call (1-32).
    /// </summary>
    [Range(1, MaxBatchSize)]
    public int BatchSize { get; set; } = MaxBatchSize;

    /// <summary>
    /// How long a received message stays invisible to other consumers. Must comfortably exceed the
    /// worst-case time to persist a batch including transient retries; a message whose visibility
    /// expires before it is deleted will be delivered again (duplicate).
    /// </summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A message that has been dequeued more than this many times is moved to the poison queue
    /// regardless of why it failed. Matches the Azure Functions default.
    /// </summary>
    [Range(1, 1000)]
    public int MaxDequeueCount { get; set; } = 5;

    /// <summary>
    /// Initial delay before polling again when the queue is empty. Doubles on every consecutive empty poll up to
    /// <see cref="EmptyQueueMaxBackoff"/>, and resets as soon as a message is received.
    /// </summary>
    public TimeSpan EmptyQueueBackoff { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Upper bound for the empty-queue backoff.
    /// </summary>
    public TimeSpan EmptyQueueMaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initial delay before polling again after a failed receive call (or failed queue creation). Doubles on every
    /// consecutive failure up to <see cref="ReceiveFailureMaxBackoff"/>.
    /// </summary>
    public TimeSpan ReceiveFailureBackoff { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Upper bound for the receive-failure backoff.
    /// </summary>
    public TimeSpan ReceiveFailureMaxBackoff { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Number of in-process attempts to persist a batch when the failure is transient (DB unavailable,
    /// timeout, missing partition, ...). After the last attempt the messages are left on the queue and
    /// become visible again after <see cref="VisibilityTimeout"/>.
    /// </summary>
    [Range(1, 20)]
    public int TransientRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff between transient retries (1x, 2x, 4x, ... plus jitter).
    /// </summary>
    public TimeSpan TransientRetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Number of consecutive batches that failed with a transient error before the consumer pauses
    /// receiving for <see cref="CircuitBreakOpenDuration"/>. This prevents a database outage from
    /// burning the dequeue count of the whole backlog and pushing it to the poison queue.
    /// </summary>
    [Range(1, 1000)]
    public int CircuitBreakFailuresBeforeOpen { get; set; } = 5;

    /// <summary>
    /// How long the consumer pauses when the circuit is open.
    /// </summary>
    public TimeSpan CircuitBreakOpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// After a stop signal, an in-flight batch gets this long to commit and delete its messages
    /// before it is cancelled. Should be below the host shutdown timeout.
    /// </summary>
    public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Gets the effective poison queue name.
    /// </summary>
    public string EffectivePoisonQueueName
        => string.IsNullOrEmpty(PoisonQueueName) ? $"{QueueName}-poison" : PoisonQueueName;
}
