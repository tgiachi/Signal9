using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Types;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Manages in-memory job queueing, state, progress, logs, and cancellation.
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Queues a job for execution.
    /// </summary>
    /// <param name="command">The job command to enqueue.</param>
    /// <param name="cancellationToken">Token used to cancel queueing.</param>
    /// <returns>The queued job snapshot.</returns>
    Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all known jobs.
    /// </summary>
    /// <returns>The known job snapshots.</returns>
    IReadOnlyList<JobSnapshot> List();

    /// <summary>
    /// Gets a job by id.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The job snapshot when found; otherwise, null.</returns>
    JobSnapshot? GetById(Guid jobId);

    /// <summary>
    /// Gets retained log entries for a job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The retained log entries.</returns>
    IReadOnlyList<JobLogEntry> GetLogs(Guid jobId);

    /// <summary>
    /// Requests cancellation for a queued or running job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">Token used to cancel the cancellation request.</param>
    /// <returns>True when cancellation was requested; otherwise, false.</returns>
    Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the next queued job id.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop waiting.</param>
    /// <returns>The next queued job id.</returns>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the next queued job id on the specified stream target.
    /// </summary>
    /// <param name="target">The stream to pull from.</param>
    /// <param name="cancellationToken">Token used to stop waiting.</param>
    /// <returns>The next queued job id.</returns>
    ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a queued job as running and returns an execution context.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">Token used to cancel the state transition.</param>
    /// <returns>The execution context when the job can run; otherwise, null.</returns>
    Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a running job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">Token used to cancel the state transition.</param>
    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails a running job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="exception">The failure exception.</param>
    /// <param name="cancellationToken">Token used to cancel the state transition.</param>
    Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a running job as canceled.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">Token used to cancel the state transition.</param>
    Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports job progress.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="percent">The progress percent.</param>
    /// <param name="message">The progress message.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a retained job log entry.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="level">The log level.</param>
    /// <param name="message">The log message.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cancellation token for a running job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The job cancellation token, or CancellationToken.None when missing.</returns>
    CancellationToken GetCancellationToken(Guid jobId);
}
