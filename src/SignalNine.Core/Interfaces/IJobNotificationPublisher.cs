using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Publishes job status and log updates to external subscribers.
/// </summary>
public interface IJobNotificationPublisher
{
    /// <summary>
    /// Publishes a job status snapshot.
    /// </summary>
    /// <param name="snapshot">The job snapshot to publish.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a job log entry.
    /// </summary>
    /// <param name="entry">The log entry to publish.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default);
}
