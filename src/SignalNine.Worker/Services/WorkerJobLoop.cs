// src/SignalNine.Worker/Services/WorkerJobLoop.cs
using Microsoft.Extensions.Hosting;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Worker.Services;

public sealed class WorkerJobLoop : BackgroundService
{
    private const int MinimumConcurrentJobs = 1;

    private readonly SemaphoreSlim _concurrency;
    private readonly Dictionary<string, IJobHandler> _handlers;
    private readonly IJobQueue _queue;
    private readonly IJobBus _bus;
    private readonly WorkerIdentity _identity;

    public WorkerJobLoop(
        SignalNineConfig config,
        IEnumerable<IJobHandler> handlers,
        IJobQueue queue,
        IJobBus bus,
        WorkerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(identity);

        _concurrency = new SemaphoreSlim(Math.Max(MinimumConcurrentJobs, config.JobSystem.MaxConcurrentJobs));
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);
        _queue = queue;
        _bus = bus;
        _identity = identity;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerName = $"worker:{_identity.Id}";
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                QueuedJob? queued;
                try
                {
                    queued = await _queue.PullAsync(consumerName, JobStreamTarget.Workers, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                if (queued is null) continue;

                await _concurrency.WaitAsync(stoppingToken).ConfigureAwait(false);
                _ = Task.Run(async () =>
                {
                    try { await ExecuteJobAsync(queued, stoppingToken).ConfigureAwait(false); }
                    finally { _concurrency.Release(); }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ExecuteJobAsync(QueuedJob queued, CancellationToken stoppingToken)
    {
        var env = queued.Envelope;
        var context = new JobExecutionContext(env.JobId, env.PayloadJson, NoopJobManager.Instance);

        if (!_handlers.TryGetValue(env.Type, out var handler))
        {
            await PublishResult(env.JobId, JobTerminalState.Failed, "No handler registered for job type.");
            await _queue.AckAsync(queued.StreamId, JobStreamTarget.Workers, stoppingToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await handler.ExecuteAsync(context, stoppingToken).ConfigureAwait(false);
            await PublishResult(env.JobId, JobTerminalState.Completed, null);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await PublishResult(env.JobId, JobTerminalState.Canceled, null);
        }
        catch (Exception ex)
        {
            await PublishResult(env.JobId, JobTerminalState.Failed, ex.Message);
        }
        finally
        {
            await _queue.AckAsync(queued.StreamId, JobStreamTarget.Workers, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task PublishResult(Guid jobId, JobTerminalState state, string? error)
        => _bus.PublishResultAsync(new JobResultEvent(jobId, state, error, ResultJson: null, At: DateTimeOffset.UtcNow));

    /// <summary>
    /// Stub IJobManager passed to handlers. In Phase 3 we still let handlers call legacy methods
    /// (WriteLogAsync etc) — these become no-ops on the worker side. Phase 4 changes the handler
    /// contract to use IJobBus directly and removes this shim.
    /// </summary>
    private sealed class NoopJobManager : IJobManager
    {
        public static readonly NoopJobManager Instance = new();
        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand c, CancellationToken ct = default) => Task.FromResult(new JobSnapshot());
        public IReadOnlyList<JobSnapshot> List() => Array.Empty<JobSnapshot>();
        public JobSnapshot? GetById(Guid id) => null;
        public IReadOnlyList<JobLogEntry> GetLogs(Guid id) => Array.Empty<JobLogEntry>();
        public Task<bool> CancelAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public ValueTask<Guid> DequeueAsync(CancellationToken ct) => ValueTask.FromResult(Guid.Empty);
        public ValueTask<Guid> DequeueAsync(JobStreamTarget t, CancellationToken ct) => ValueTask.FromResult(Guid.Empty);
        public Task<JobExecutionContext?> StartAsync(Guid id, CancellationToken ct = default) => Task.FromResult<JobExecutionContext?>(null);
        public Task CompleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task FailAsync(Guid id, Exception ex, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkCanceledAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReportProgressAsync(Guid id, int p, string m, CancellationToken ct = default) => Task.CompletedTask;
        public Task WriteLogAsync(Guid id, JobLogLevelType l, string m, CancellationToken ct = default) => Task.CompletedTask;
        public CancellationToken GetCancellationToken(Guid id) => CancellationToken.None;
    }
}
