using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using ILogger = Serilog.ILogger;

namespace SignalNine.Worker.Services;

public sealed class WorkerHeartbeatService : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger _logger = Log.ForContext<WorkerHeartbeatService>();
    private readonly IJobBus _bus;
    private readonly WorkerIdentity _identity;
    private readonly WorkerRuntimeState _state;
    private readonly TimeSpan _interval;

    public WorkerHeartbeatService(IJobBus bus, WorkerIdentity identity, WorkerRuntimeState state)
        : this(bus, identity, state, DefaultInterval)
    {
    }

    // Test-only overload — allows fast ticks.
    public WorkerHeartbeatService(IJobBus bus, WorkerIdentity identity, WorkerRuntimeState state, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        _bus = bus;
        _identity = identity;
        _state = state;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var version = typeof(WorkerHeartbeatService).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = new WorkerHeartbeat(
                    _identity.Id,
                    _identity.Name,
                    version,
                    _state.RunningCount,
                    _state.Capacity,
                    _state.Snapshot(),
                    DateTimeOffset.UtcNow
                );
                await _bus.PublishHeartbeatAsync(heartbeat, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to publish worker heartbeat.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
