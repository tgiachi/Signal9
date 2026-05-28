using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobExecutionContext
{
    private readonly IJobBus _bus;

    public Guid JobId { get; }

    public string PayloadJson { get; }

    public string WorkDir { get; }

    public JobExecutionContext(Guid jobId, string payloadJson, string workDir, IJobBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentException.ThrowIfNullOrEmpty(workDir);

        JobId = jobId;
        PayloadJson = payloadJson;
        WorkDir = workDir;
        _bus = bus;
    }

    public Task ReportProgressAsync(int percent, string message, CancellationToken cancellationToken = default)
    {
        return _bus.PublishProgressAsync(
            new JobProgressEvent(JobId, percent, message, DateTimeOffset.UtcNow),
            cancellationToken
        );
    }

    public Task WriteLogAsync(JobLogLevelType level, string message, CancellationToken cancellationToken = default)
    {
        return _bus.PublishLogAsync(
            new JobLogEvent(JobId, level, message, DateTimeOffset.UtcNow),
            cancellationToken
        );
    }
}
