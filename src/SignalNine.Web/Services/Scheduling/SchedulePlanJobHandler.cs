using System.Text.Json;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Schedule;

namespace SignalNine.Web.Services.Scheduling;

public sealed class SchedulePlanJobHandler : IJobHandler
{
    public const string JobType = "schedule.plan";

    private readonly SchedulePlannerService _planner;

    public SchedulePlanJobHandler(SchedulePlannerService planner)
    {
        ArgumentNullException.ThrowIfNull(planner);
        _planner = planner;
    }

    public string Type
    {
        get { return JobType; }
    }

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = JsonSerializer.Deserialize<SchedulePlanJobPayload>(context.PayloadJson)
                      ?? throw new InvalidOperationException("Empty schedule.plan payload.");

        var toUtc = payload.FromUtc.AddHours(Math.Max(1, payload.HoursAhead));
        var written = await _planner
            .PlanChannelAsync(payload.ChannelId, payload.FromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        await context.WriteLogAsync(
            SignalNine.Core.Types.JobLogLevelType.Information,
            $"SchedulePlannerService.PlanChannelAsync wrote {written} entries for channel {payload.ChannelId}.",
            cancellationToken
        ).ConfigureAwait(false);
    }
}
