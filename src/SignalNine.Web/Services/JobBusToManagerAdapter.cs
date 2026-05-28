using Microsoft.Extensions.Logging;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Web.Services;

/// <summary>
/// Subscribes to <see cref="IJobBus"/> events and forwards them to <see cref="IJobManager"/>.
/// On completion, dispatches the result to the matching <see cref="IJobResultProcessor"/> by JobType.
/// </summary>
public sealed class JobBusToManagerAdapter : BackgroundService
{
    private readonly IJobBus _bus;
    private readonly IJobManager _jobManager;
    private readonly IEnumerable<IJobResultProcessor> _processors;
    private readonly ILogger<JobBusToManagerAdapter> _logger;

    public JobBusToManagerAdapter(
        IJobBus bus,
        IJobManager jobManager,
        IEnumerable<IJobResultProcessor> processors,
        ILogger<JobBusToManagerAdapter> logger)
    {
        _bus = bus;
        _jobManager = jobManager;
        _processors = processors;
        _logger = logger;
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
            switch (e.State)
            {
                case JobTerminalState.Completed:
                    var jobType = _jobManager.GetById(e.JobId)?.Type;
                    if (jobType is null)
                    {
                        _logger.LogWarning(
                            "Received Completed result for unknown job {JobId} — no type resolved, skipping processor.",
                            e.JobId);
                    }
                    else
                    {
                        var processor = _processors.FirstOrDefault(p => p.JobType == jobType);
                        if (processor is not null)
                        {
                            try
                            {
                                await processor.ApplyAsync(e.JobId, e.ResultJson, ct).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Result processor {ProcessorType} threw for job {JobId} (type={JobType}).",
                                    processor.GetType().Name, e.JobId, jobType);
                            }
                        }
                    }
                    await _jobManager.CompleteAsync(e.JobId, ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Failed:
                    await _jobManager.FailAsync(e.JobId, new Exception(e.Error ?? "unknown"), ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Canceled:
                    await _jobManager.MarkCanceledAsync(e.JobId, ct).ConfigureAwait(false);
                    break;
                case JobTerminalState.Retry:
                    // Phase 6 handles retry. Phase 1 just logs.
                    await _jobManager.WriteLogAsync(e.JobId, JobLogLevelType.Warning,
                        $"Retry requested: {e.Error}", ct).ConfigureAwait(false);
                    break;
            }
        }
    }
}
