using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Handles one job type.
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// Gets the job type handled by this handler.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Executes the job.
    /// </summary>
    /// <param name="context">The job execution context.</param>
    /// <param name="cancellationToken">Token used to stop the job.</param>
    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}
