using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Interfaces;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services;

internal sealed class HeartbeatConsumer : BackgroundService
{
    private readonly ILogger _logger = Log.ForContext<HeartbeatConsumer>();
    private readonly IJobBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;

    public HeartbeatConsumer(IJobBus bus, IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _bus = bus;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var heartbeat in _bus.SubscribeHeartbeatAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var registry = scope.ServiceProvider.GetRequiredService<IWorkerRegistry>();
                    await registry.UpsertHeartbeatAsync(heartbeat, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to persist worker heartbeat for {WorkerId}", heartbeat.WorkerId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }
}
