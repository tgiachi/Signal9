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
    private readonly WorkerRuntimeState _state;

    public WorkerJobLoop(
        SignalNineConfig config,
        IEnumerable<IJobHandler> handlers,
        IJobQueue queue,
        IJobBus bus,
        WorkerIdentity identity,
        WorkerRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        _concurrency = new SemaphoreSlim(Math.Max(MinimumConcurrentJobs, config.JobSystem.MaxConcurrentJobs));
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);
        _queue = queue;
        _bus = bus;
        _identity = identity;
        _state = state;
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
        // TODO(T16-T18): replace temp path with WorkSpaceConfig.Path once wiring is complete
        var workDir = Path.Combine(Path.GetTempPath(), $"signalnine-worker-{env.JobId:N}");
        Directory.CreateDirectory(workDir);
        var context = new JobExecutionContext(env.JobId, env.PayloadJson, workDir, _bus);

        if (!_handlers.TryGetValue(env.Type, out var handler))
        {
            await PublishResult(env.JobId, JobTerminalState.Failed, "No handler registered for job type.", null);
            await _queue.AckAsync(queued.StreamId, JobStreamTarget.Workers, stoppingToken).ConfigureAwait(false);
            return;
        }

        _state.MarkStarted(env.JobId);
        try
        {
            var result = await handler.ExecuteAsync(context, stoppingToken).ConfigureAwait(false);
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result, result.GetType());
            await PublishResult(env.JobId, JobTerminalState.Completed, null, resultJson);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await PublishResult(env.JobId, JobTerminalState.Canceled, null, null);
        }
        catch (Exception ex)
        {
            await PublishResult(env.JobId, JobTerminalState.Failed, ex.Message, null);
        }
        finally
        {
            _state.MarkFinished(env.JobId);
            await _queue.AckAsync(queued.StreamId, JobStreamTarget.Workers, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task PublishResult(Guid jobId, JobTerminalState state, string? error, string? resultJson)
        => _bus.PublishResultAsync(new JobResultEvent(jobId, state, error, ResultJson: resultJson, At: DateTimeOffset.UtcNow));
}
