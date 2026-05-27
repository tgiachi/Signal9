using System.Collections.Concurrent;
using System.Threading.Channels;

using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Core.Services;

public class InMemoryJobManager : IJobManager
{
    private const int MinimumLogEntriesPerJob = 1;

    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly ConcurrentDictionary<Guid, object> _jobLocks = new();
    private readonly ConcurrentDictionary<Guid, JobSnapshot> _jobs = new();
    private readonly ConcurrentDictionary<Guid, List<JobLogEntry>> _logs = new();
    private readonly int _maxLogEntriesPerJob;
    private readonly IJobNotificationPublisher _notificationPublisher;
    private long _logSequence;

    public InMemoryJobManager(SignalNineConfig config, IJobNotificationPublisher notificationPublisher)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(notificationPublisher);

        _maxLogEntriesPerJob = Math.Max(MinimumLogEntriesPerJob, config.JobSystem.MaxLogEntriesPerJob);
        _notificationPublisher = notificationPublisher;
    }

    public async Task<JobSnapshot> EnqueueAsync(
        EnqueueJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            throw new ArgumentException("Job type cannot be empty.", nameof(command));
        }

        var now = DateTimeOffset.UtcNow;
        var snapshot = new JobSnapshot
        {
            Id = Guid.NewGuid(),
            Type = command.Type,
            PayloadJson = string.IsNullOrWhiteSpace(command.PayloadJson) ? "{}" : command.PayloadJson,
            State = JobStateType.Queued,
            CreatedAt = now,
            Progress =
            {
                UpdatedAt = now
            }
        };

        _jobs[snapshot.Id] = snapshot;
        _jobLocks[snapshot.Id] = new object();
        await _queue.Writer.WriteAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);

        return Clone(snapshot);
    }

    public IReadOnlyList<JobSnapshot> List()
        => _jobs.Values.Select(Clone).OrderByDescending(job => job.CreatedAt).ToList();

    public JobSnapshot? GetById(Guid jobId)
        => _jobs.TryGetValue(jobId, out var snapshot) ? Clone(snapshot) : null;

    public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
    {
        if (!_logs.TryGetValue(jobId, out var entries))
        {
            return [];
        }

        lock (entries)
        {
            return entries.Select(Clone).ToList();
        }
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot))
        {
            return false;
        }

        JobSnapshot clonedSnapshot;

        lock (GetJobLock(jobId))
        {
            if (IsTerminal(snapshot.State))
            {
                return false;
            }

            if (_cancellationTokens.TryGetValue(jobId, out var source))
            {
                source.Cancel();
            }

            snapshot.State = JobStateType.Canceled;
            snapshot.FinishedAt = DateTimeOffset.UtcNow;
            clonedSnapshot = Clone(snapshot);
        }

        await _notificationPublisher.PublishStatusAsync(clonedSnapshot, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => _queue.Reader.ReadAsync(cancellationToken);

    public async Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot))
        {
            return null;
        }

        JobSnapshot clonedSnapshot;

        lock (GetJobLock(jobId))
        {
            if (snapshot.State != JobStateType.Queued)
            {
                return null;
            }

            snapshot.State = JobStateType.Running;
            snapshot.StartedAt = DateTimeOffset.UtcNow;
            _cancellationTokens[jobId] = new CancellationTokenSource();
            clonedSnapshot = Clone(snapshot);
        }

        await _notificationPublisher.PublishStatusAsync(clonedSnapshot, cancellationToken).ConfigureAwait(false);

        return new JobExecutionContext(jobId, clonedSnapshot.PayloadJson, this);
    }

    public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        => MoveToTerminalAsync(jobId, JobStateType.Completed, "", cancellationToken);

    public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return MoveToTerminalAsync(jobId, JobStateType.Failed, exception.Message, cancellationToken);
    }

    public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
        => MoveToTerminalAsync(jobId, JobStateType.Canceled, "", cancellationToken);

    public async Task ReportProgressAsync(
        Guid jobId,
        int percent,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot))
        {
            return;
        }

        JobSnapshot clonedSnapshot;

        lock (GetJobLock(jobId))
        {
            if (IsTerminal(snapshot.State))
            {
                return;
            }

            snapshot.Progress.Percent = Math.Clamp(percent, 0, 100);
            snapshot.Progress.Message = message;
            snapshot.Progress.UpdatedAt = DateTimeOffset.UtcNow;
            clonedSnapshot = Clone(snapshot);
        }

        await _notificationPublisher.PublishStatusAsync(clonedSnapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteLogAsync(
        Guid jobId,
        JobLogLevelType level,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (!_jobs.ContainsKey(jobId))
        {
            return;
        }

        var entry = new JobLogEntry
        {
            Sequence = Interlocked.Increment(ref _logSequence),
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message
        };
        var entries = _logs.GetOrAdd(jobId, _ => []);

        lock (entries)
        {
            entries.Add(entry);

            while (entries.Count > _maxLogEntriesPerJob)
            {
                entries.RemoveAt(0);
            }
        }

        await _notificationPublisher.PublishLogAsync(Clone(entry), cancellationToken).ConfigureAwait(false);
    }

    public CancellationToken GetCancellationToken(Guid jobId)
        => _cancellationTokens.TryGetValue(jobId, out var source) ? source.Token : CancellationToken.None;

    private async Task MoveToTerminalAsync(
        Guid jobId,
        JobStateType state,
        string error,
        CancellationToken cancellationToken
    )
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot))
        {
            return;
        }

        JobSnapshot clonedSnapshot;

        lock (GetJobLock(jobId))
        {
            if (snapshot.State == JobStateType.Canceled && state == JobStateType.Completed)
            {
                return;
            }

            snapshot.State = state;
            snapshot.Error = error;
            snapshot.FinishedAt = DateTimeOffset.UtcNow;
            clonedSnapshot = Clone(snapshot);
        }

        if (_cancellationTokens.TryRemove(jobId, out var source))
        {
            source.Dispose();
        }

        await _notificationPublisher.PublishStatusAsync(clonedSnapshot, cancellationToken).ConfigureAwait(false);
    }

    private object GetJobLock(Guid jobId)
        => _jobLocks.GetOrAdd(jobId, _ => new object());

    private static bool IsTerminal(JobStateType state)
        => state is JobStateType.Completed or JobStateType.Failed or JobStateType.Canceled;

    private static JobSnapshot Clone(JobSnapshot snapshot)
        => new()
        {
            Id = snapshot.Id,
            Type = snapshot.Type,
            PayloadJson = snapshot.PayloadJson,
            State = snapshot.State,
            Progress = new JobProgressSnapshot
            {
                Percent = snapshot.Progress.Percent,
                Message = snapshot.Progress.Message,
                UpdatedAt = snapshot.Progress.UpdatedAt
            },
            Error = snapshot.Error,
            CreatedAt = snapshot.CreatedAt,
            StartedAt = snapshot.StartedAt,
            FinishedAt = snapshot.FinishedAt
        };

    private static JobLogEntry Clone(JobLogEntry entry)
        => new()
        {
            Sequence = entry.Sequence,
            JobId = entry.JobId,
            Timestamp = entry.Timestamp,
            Level = entry.Level,
            Message = entry.Message
        };
}
