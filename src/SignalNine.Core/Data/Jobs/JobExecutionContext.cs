using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobExecutionContext
{
    private readonly IJobManager _jobManager;

    public Guid JobId { get; }

    public string PayloadJson { get; }

    public JobExecutionContext(Guid jobId, string payloadJson, IJobManager jobManager)
    {
        ArgumentNullException.ThrowIfNull(jobManager);

        JobId = jobId;
        PayloadJson = payloadJson;
        _jobManager = jobManager;
    }

    public Task ReportProgressAsync(int percent, string message, CancellationToken cancellationToken = default)
        => _jobManager.ReportProgressAsync(JobId, percent, message, cancellationToken);

    public Task WriteLogAsync(JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        => _jobManager.WriteLogAsync(JobId, level, message, cancellationToken);
}
