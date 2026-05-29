using Microsoft.Extensions.DependencyInjection;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;

namespace SignalNine.Web.Services.Scheduling;

public sealed class SchedulePlannerService
{
    private const int DefaultBumperDurationSeconds = 8;
    private const int DefaultCommercialDurationSeconds = 30;

    private readonly IServiceScopeFactory _scopeFactory;

    public SchedulePlannerService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<int> PlanChannelAsync(
        Guid channelId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public sealed record ResolveContext(
        IReadOnlyList<ChannelMediaEntity> AllMedia,
        IReadOnlyDictionary<Guid, HashSet<string>> TagsByMedia,
        DateTime Cursor
    );

    public static Guid? ResolveBlock(ScheduleBlockEntity block, ResolveContext ctx)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(ctx);

        return block.RuleType switch
        {
            ScheduleBlockRuleType.Pin => block.PinnedChannelMediaId,
            ScheduleBlockRuleType.Series => ResolveSeries(block, ctx),
            _ => null
        };
    }

    private static Guid? ResolveSeries(ScheduleBlockEntity block, ResolveContext ctx)
    {
        if (string.IsNullOrWhiteSpace(block.SeriesName)) return null;
        var episodes = ctx.AllMedia
            .Where(m => m.IsActive
                        && m.Type == ChannelMediaType.TvShow
                        && string.Equals(m.TvSeriesName, block.SeriesName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.TvSeason ?? 0)
            .ThenBy(m => m.TvEpisode ?? 0)
            .ToList();
        if (episodes.Count == 0) return null;
        if (block.SeriesCursorChannelMediaId is null) return episodes[0].Id;
        var idx = episodes.FindIndex(m => m.Id == block.SeriesCursorChannelMediaId.Value);
        if (idx < 0) return episodes[0].Id;
        return episodes[(idx + 1) % episodes.Count].Id;
    }

    public static ScheduleBlockEntity? FindBlockCovering(
        IEnumerable<ScheduleBlockEntity> blocks,
        DateTime cursor)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        foreach (var block in blocks)
        {
            if (!block.IsActive) continue;
            if (block.DayOfWeek != cursor.DayOfWeek) continue;
            var startSeconds = (int)block.StartTime.TotalSeconds;
            var endSeconds = startSeconds + block.DurationMinutes * 60;
            var cursorSeconds = (int)cursor.TimeOfDay.TotalSeconds;
            if (cursorSeconds >= startSeconds && cursorSeconds < endSeconds)
            {
                return block;
            }
        }

        return null;
    }
}
