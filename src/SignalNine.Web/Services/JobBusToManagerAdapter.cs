using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Web.Services;

/// <summary>
/// Phase 1 wiring. Subscribes to <see cref="IJobBus"/> events and forwards them to
/// <see cref="IJobManager"/>. In Phase 1 the handlers still mutate state directly, so
/// the bus is idle and this service does nothing visible. Phase 4 wires real progress/
/// log/result publishing from handlers, and this adapter becomes the single ingestion
/// point on the web side.
/// </summary>
public sealed class JobBusToManagerAdapter : BackgroundService
{
    private readonly IJobBus _bus;
    private readonly IJobManager _jobManager;
    private readonly IEnumerable<IJobResultProcessor> _processors;

    public JobBusToManagerAdapter(IJobBus bus, IJobManager jobManager, IEnumerable<IJobResultProcessor> processors)
    {
        _bus = bus;
        _jobManager = jobManager;
        _processors = processors;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var progressTask = ConsumeProgressAsync(stoppingToken);
        var logTask = ConsumeLogAsync(stoppingToken);
        var resultTask = ConsumeResultAsync(stoppingToken);
        await Task.WhenAll(progressTask, logTask, resultTask).ConfigureAwait(false);
    }

    private async Task ConsumeProgressAsync(CancellationToken ct)
    {
        await foreach (var e in _bus.SubscribeProgressAsync(ct).ConfigureAwait(false))
        {
            await _jobManager.ReportProgressAsync(e.JobId, e.Percent, e.Message, ct).ConfigureAwait(false);
        }
    }

    private async Task ConsumeLogAsync(CancellationToken ct)
    {
        await foreach (var e in _bus.SubscribeLogAsync(ct).ConfigureAwait(false))
        {
            await _jobManager.WriteLogAsync(e.JobId, e.Level, e.Message, ct).ConfigureAwait(false);
        }
    }

    private async Task ConsumeResultAsync(CancellationToken ct)
    {
        await foreach (var e in _bus.SubscribeResultAsync(ct).ConfigureAwait(false))
        {
            // Phase 4 will dispatch to per-type processors. For now route to the legacy shim.
            var processor = _processors.FirstOrDefault(p => p.JobType == "*");
            if (processor is not null)
                await processor.ApplyAsync(e.JobId, e.ResultJson, ct).ConfigureAwait(false);

            switch (e.State)
            {
                case JobTerminalState.Completed:
                    await _jobManager.CompleteAsync(e.JobId, ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Failed:
                    await _jobManager.FailAsync(e.JobId, new Exception(e.Error ?? "unknown"), ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Canceled:
                    await _jobManager.MarkCanceledAsync(e.JobId, ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Retry:
                    // Phase 6 handles this. Phase 1 just logs.
                    await _jobManager.WriteLogAsync(e.JobId, JobLogLevelType.Warning,
                        $"Retry requested: {e.Error}", ct).ConfigureAwait(false);
                    break;
            }
        }
    }
}
