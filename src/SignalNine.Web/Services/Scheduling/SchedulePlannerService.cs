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
            ScheduleBlockRuleType.TagPool => ResolveTagPool(
                block.Id,
                block.TagFilterCsv,
                block.TypeFilterCsv,
                ctx),
            _ => null
        };
    }

    public static Guid? ResolveTagPool(
        Guid seedSource,
        string? tagFilterCsv,
        string? typeFilterCsv,
        ResolveContext ctx)
    {
        var allowedTypes = ParseTypeFilter(typeFilterCsv);
        var requiredTags = ParseTagFilter(tagFilterCsv);

        var candidates = ctx.AllMedia
            .Where(m => m.IsActive)
            .Where(m => allowedTypes is null || allowedTypes.Contains(m.Type))
            .Where(m =>
            {
                if (requiredTags.Count == 0) return true;
                return ctx.TagsByMedia.TryGetValue(m.Id, out var tags)
                       && requiredTags.All(rt => tags.Contains(rt));
            })
            .OrderBy(m => m.Id)
            .ToList();
        if (candidates.Count == 0) return null;

        var seed = HashCombine(seedSource, (long)(ctx.Cursor.Date - DateTime.UnixEpoch).TotalDays);
        var rng = new Random((int)(seed & 0x7FFFFFFF));
        return candidates[rng.Next(candidates.Count)].Id;
    }

    private static HashSet<ChannelMediaType>? ParseTypeFilter(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var set = new HashSet<ChannelMediaType>();
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<ChannelMediaType>(token, ignoreCase: true, out var t))
            {
                set.Add(t);
            }
        }
        return set.Count == 0 ? null : set;
    }

    private static HashSet<string> ParseTagFilter(string? csv)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv)) return set;
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            set.Add(token);
        }
        return set;
    }

    private static long HashCombine(Guid g, long extra)
    {
        var bytes = g.ToByteArray();
        unchecked
        {
            long h = (long)14695981039346656037UL;
            foreach (var b in bytes)
            {
                h ^= b;
                h *= 1099511628211L;
            }
            h ^= extra;
            h *= 1099511628211L;
            return h;
        }
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

    public static Guid? ResolveFallback(ChannelEntity channel, ResolveContext ctx)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(ctx);

        var typeFilter = channel.FallbackTypeFilterCsv;
        if (string.IsNullOrWhiteSpace(typeFilter))
        {
            typeFilter = $"{ChannelMediaType.Movies},{ChannelMediaType.TvShow}";
        }

        return ResolveTagPool(channel.Id, channel.FallbackTagFilterCsv, typeFilter, ctx);
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
