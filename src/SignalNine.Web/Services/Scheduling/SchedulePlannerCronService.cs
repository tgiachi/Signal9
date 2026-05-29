using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Schedule;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services.Scheduling;

internal sealed class SchedulePlannerCronService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);
    private const int FireHourLocal = 3;
    private const int HorizonHours = 48;

    private readonly ILogger _logger = Log.ForContext<SchedulePlannerCronService>();
    private readonly IServiceScopeFactory _scopeFactory;
    private DateTime _lastFiredDate = DateTime.MinValue;

    public SchedulePlannerCronService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                if (now.Hour >= FireHourLocal && now.Date > _lastFiredDate)
                {
                    await FireAsync(stoppingToken).ConfigureAwait(false);
                    _lastFiredDate = now.Date;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "SchedulePlannerCronService tick failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task FireAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var channels = sp.GetRequiredService<IDataAccess<ChannelEntity>>();
        var jobs = sp.GetRequiredService<IJobManager>();

        var active = channels.List().Where(c => c.IsActive).ToList();
        foreach (var channel in active)
        {
            var payload = JsonSerializer.Serialize(
                new SchedulePlanJobPayload(channel.Id, DateTime.UtcNow, HorizonHours));
            await jobs.EnqueueAsync(
                new EnqueueJobCommand { Type = SchedulePlanJobHandler.JobType, PayloadJson = payload },
                ct
            ).ConfigureAwait(false);
        }

        _logger.Information("SchedulePlannerCronService enqueued schedule.plan jobs for {Count} channels.", active.Count);
    }
}
