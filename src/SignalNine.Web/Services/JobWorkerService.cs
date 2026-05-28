using System.Text.Json;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Web.Services;

public class JobWorkerService : BackgroundService
{
    private const int MinimumConcurrentJobs = 1;

    private readonly SemaphoreSlim _concurrency;
    private readonly Dictionary<string, IJobHandler> _handlers;
    private readonly IJobManager _jobManager;
    private readonly IJobBus _bus;
    private readonly JobStreamTarget _target;

    public JobWorkerService(
        SignalNineConfig config,
        IEnumerable<IJobHandler> handlers,
        IJobManager jobManager,
        IJobBus bus,
        JobStreamTarget target
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(jobManager);
        ArgumentNullException.ThrowIfNull(bus);

        _concurrency = new SemaphoreSlim(Math.Max(MinimumConcurrentJobs, config.JobSystem.MaxConcurrentJobs));
        _handlers = handlers.ToDictionary(handler => handler.Type, StringComparer.OrdinalIgnoreCase);
        _jobManager = jobManager;
        _bus = bus;
        _target = target;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var jobId = await _jobManager.DequeueAsync(_target, stoppingToken).ConfigureAwait(false);
                await _concurrency.WaitAsync(stoppingToken).ConfigureAwait(false);

                _ = Task.Run(async () =>
                {
                    try { await ExecuteJobAsync(jobId, stoppingToken).ConfigureAwait(false); }
                    finally { _concurrency.Release(); }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ExecuteJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        var context = await _jobManager.StartAsync(jobId, stoppingToken).ConfigureAwait(false);
        if (context is null) return;

        var snapshot = _jobManager.GetById(jobId);
        if (snapshot is null || !_handlers.TryGetValue(snapshot.Type, out var handler))
        {
            await _jobManager.WriteLogAsync(jobId, JobLogLevelType.Error,
                "No handler registered for job type.", stoppingToken).ConfigureAwait(false);
            await _jobManager.FailAsync(jobId,
                new InvalidOperationException("No handler registered for job type."),
                stoppingToken).ConfigureAwait(false);
            return;
        }

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, _jobManager.GetCancellationToken(jobId));

        try
        {
            var result = await handler.ExecuteAsync(context, linkedSource.Token).ConfigureAwait(false);
            var resultJson = JsonSerializer.Serialize(result, result.GetType());
            await _bus.PublishResultAsync(new JobResultEvent(
                jobId, JobTerminalState.Completed, null, resultJson, DateTimeOffset.UtcNow
            )).ConfigureAwait(false);
            await _jobManager.CompleteAsync(jobId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _jobManager.MarkCanceledAsync(jobId, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _jobManager.FailAsync(jobId, ex, stoppingToken).ConfigureAwait(false);
        }
    }
}
