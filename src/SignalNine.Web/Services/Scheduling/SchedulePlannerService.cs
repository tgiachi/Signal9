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

    public static int EmitMediaWithBreaks(
        ChannelEntity channel,
        ChannelMediaEntity media,
        DateTime startUtc,
        int maxDurationSeconds,
        Guid? sourceBlockId,
        IReadOnlyList<ChannelMediaEntity> adsPool,
        IReadOnlyList<ChannelMediaEntity> bumpersPool,
        Random rng,
        List<ScheduledEntryEntity> sink)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(adsPool);
        ArgumentNullException.ThrowIfNull(bumpersPool);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(sink);

        var mediaDuration = Math.Min(media.DurationSeconds ?? maxDurationSeconds, maxDurationSeconds);
        if (mediaDuration <= 0) return 0;

        var interval = Math.Max(1, channel.CommercialIntervalMinSeconds);
        var jitter = Math.Max(0, channel.CommercialIntervalJitterSeconds);
        var breakSize = Math.Max(0, channel.CommercialBreakSize);
        var totalParts = channel.CommercialsEnabled
            ? Math.Max(1, (int)Math.Ceiling((double)mediaDuration / interval))
            : 1;

        var cursor = startUtc;
        var remaining = mediaDuration;
        var partIndex = 0;
        var offset = 0;

        while (remaining > 0)
        {
            var chunkPlanned = channel.CommercialsEnabled
                ? interval + (jitter > 0 ? rng.Next(-jitter, jitter + 1) : 0)
                : remaining;
            var chunk = Math.Min(chunkPlanned, remaining);
            if (chunk <= 0) chunk = remaining;

            sink.Add(new ScheduledEntryEntity
            {
                Id = Guid.NewGuid(),
                ChannelId = channel.Id,
                SourceBlockId = sourceBlockId,
                StartAt = cursor,
                DurationSeconds = chunk,
                Kind = ScheduledEntryKind.Media,
                ChannelMediaId = media.Id,
                MediaPartIndex = partIndex,
                MediaPartCount = totalParts,
                MediaOffsetSeconds = offset
            });

            cursor = cursor.AddSeconds(chunk);
            remaining -= chunk;
            offset += chunk;
            partIndex++;

            if (remaining > 0 && channel.CommercialsEnabled)
            {
                if (channel.CommercialBumpersEnabled && bumpersPool.Count > 0)
                {
                    cursor = EmitOne(channel, sourceBlockId, bumpersPool[rng.Next(bumpersPool.Count)], cursor, ScheduledEntryKind.Bumper, sink, DefaultBumperDurationSeconds);
                }
                for (var i = 0; i < breakSize; i++)
                {
                    if (adsPool.Count == 0) break;
                    cursor = EmitOne(channel, sourceBlockId, adsPool[rng.Next(adsPool.Count)], cursor, ScheduledEntryKind.Commercial, sink, DefaultCommercialDurationSeconds);
                }
                if (channel.CommercialBumpersEnabled && bumpersPool.Count > 0)
                {
                    cursor = EmitOne(channel, sourceBlockId, bumpersPool[rng.Next(bumpersPool.Count)], cursor, ScheduledEntryKind.Bumper, sink, DefaultBumperDurationSeconds);
                }
            }
        }

        return (int)(cursor - startUtc).TotalSeconds;
    }

    private static DateTime EmitOne(
        ChannelEntity channel,
        Guid? sourceBlockId,
        ChannelMediaEntity item,
        DateTime cursor,
        ScheduledEntryKind kind,
        List<ScheduledEntryEntity> sink,
        int defaultDurationSeconds)
    {
        var dur = item.DurationSeconds is > 0 ? item.DurationSeconds.Value : defaultDurationSeconds;
        sink.Add(new ScheduledEntryEntity
        {
            Id = Guid.NewGuid(),
            ChannelId = channel.Id,
            SourceBlockId = sourceBlockId,
            StartAt = cursor,
            DurationSeconds = dur,
            Kind = kind,
            ChannelMediaId = item.Id,
            MediaPartIndex = 0,
            MediaPartCount = 1,
            MediaOffsetSeconds = 0
        });
        return cursor.AddSeconds(dur);
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
