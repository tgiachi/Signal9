using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Support;

public sealed class NoopJobManager : IJobManager
{
    public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyList<JobSnapshot> List()
    {
        return Array.Empty<JobSnapshot>();
    }

    public JobSnapshot? GetById(Guid jobId)
    {
        return null;
    }

    public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
    {
        return Array.Empty<JobLogEntry>();
    }

    public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
        => DequeueAsync(cancellationToken);

    public Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public CancellationToken GetCancellationToken(Guid jobId)
    {
        return CancellationToken.None;
    }
}
