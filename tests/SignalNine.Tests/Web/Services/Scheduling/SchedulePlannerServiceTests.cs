using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services.Scheduling;

namespace SignalNine.Tests.Web.Services.Scheduling;

public class SchedulePlannerServiceTests
{
    [Fact]
    public void FindBlockCovering_BlockMatchesDayAndTime_ReturnsBlock()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        // 2026-06-01 is a Monday; 21:00 falls inside 20:00–22:00.
        var cursor = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc);

        var hit = SchedulePlannerService.FindBlockCovering(new[] { block }, cursor);

        Assert.Same(block, hit);
    }

    [Fact]
    public void FindBlockCovering_CursorBeforeStart_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        var cursor = new DateTime(2026, 6, 1, 19, 59, 59, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }

    [Fact]
    public void FindBlockCovering_CursorPastEnd_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        var cursor = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }

    [Fact]
    public void FindBlockCovering_InactiveBlock_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = false
        };
        var cursor = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }

    [Fact]
    public void ResolveBlock_Pin_ReturnsPinnedMediaId()
    {
        var pinned = Guid.NewGuid();
        var block = new ScheduleBlockEntity
        {
            RuleType = ScheduleBlockRuleType.Pin,
            PinnedChannelMediaId = pinned
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            AllMedia: Array.Empty<ChannelMediaEntity>(),
            TagsByMedia: new Dictionary<Guid, HashSet<string>>(),
            Cursor: DateTime.UtcNow);

        Assert.Equal(pinned, SchedulePlannerService.ResolveBlock(block, ctx));
    }

    [Fact]
    public void ResolveBlock_Pin_NullPinnedId_ReturnsNull()
    {
        var block = new ScheduleBlockEntity { RuleType = ScheduleBlockRuleType.Pin, PinnedChannelMediaId = null };
        var ctx = new SchedulePlannerService.ResolveContext(
            AllMedia: Array.Empty<ChannelMediaEntity>(),
            TagsByMedia: new Dictionary<Guid, HashSet<string>>(),
            Cursor: DateTime.UtcNow);

        Assert.Null(SchedulePlannerService.ResolveBlock(block, ctx));
    }

    private static ChannelMediaEntity Ep(string series, int season, int episode, Guid? id = null)
    {
        return new ChannelMediaEntity
        {
            Id = id ?? Guid.NewGuid(),
            Type = ChannelMediaType.TvShow,
            TvSeriesName = series,
            TvSeason = season,
            TvEpisode = episode,
            IsActive = true
        };
    }

    [Fact]
    public void ResolveBlock_Series_FirstRun_ReturnsFirstEpisode()
    {
        var e1 = Ep("Ken", 1, 1);
        var e2 = Ep("Ken", 1, 2);
        var block = new ScheduleBlockEntity
        {
            RuleType = ScheduleBlockRuleType.Series,
            SeriesName = "Ken",
            SeriesCursorChannelMediaId = null
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { e2, e1 },
            new Dictionary<Guid, HashSet<string>>(),
            DateTime.UtcNow);

        Assert.Equal(e1.Id, SchedulePlannerService.ResolveBlock(block, ctx));
    }

    [Fact]
    public void ResolveBlock_Series_AfterCursor_ReturnsNextEpisode()
    {
        var e1 = Ep("Ken", 1, 1);
        var e2 = Ep("Ken", 1, 2);
        var e3 = Ep("Ken", 1, 3);
        var block = new ScheduleBlockEntity
        {
            RuleType = ScheduleBlockRuleType.Series,
            SeriesName = "Ken",
            SeriesCursorChannelMediaId = e2.Id
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { e1, e2, e3 },
            new Dictionary<Guid, HashSet<string>>(),
            DateTime.UtcNow);

        Assert.Equal(e3.Id, SchedulePlannerService.ResolveBlock(block, ctx));
    }

    [Fact]
    public void ResolveBlock_Series_LastEpisode_WrapsToFirst()
    {
        var e1 = Ep("Ken", 1, 1);
        var e2 = Ep("Ken", 1, 2);
        var block = new ScheduleBlockEntity
        {
            RuleType = ScheduleBlockRuleType.Series,
            SeriesName = "Ken",
            SeriesCursorChannelMediaId = e2.Id
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { e1, e2 },
            new Dictionary<Guid, HashSet<string>>(),
            DateTime.UtcNow);

        Assert.Equal(e1.Id, SchedulePlannerService.ResolveBlock(block, ctx));
    }

    [Fact]
    public void ResolveBlock_TagPool_OnlyMatchingMedia_PicksOne()
    {
        var match = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Movies, IsActive = true };
        var skip = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.TvShow, IsActive = true };
        var block = new ScheduleBlockEntity
        {
            Id = Guid.NewGuid(),
            RuleType = ScheduleBlockRuleType.TagPool,
            TagFilterCsv = "action",
            TypeFilterCsv = "Movies"
        };
        var tags = new Dictionary<Guid, HashSet<string>>
        {
            [match.Id] = new(StringComparer.OrdinalIgnoreCase) { "action", "sci-fi" },
            [skip.Id] = new(StringComparer.OrdinalIgnoreCase) { "drama" }
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { match, skip }, tags, new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc));

        Assert.Equal(match.Id, SchedulePlannerService.ResolveBlock(block, ctx));
    }

    [Fact]
    public void ResolveBlock_TagPool_DeterministicAcrossRuns()
    {
        var ids = Enumerable.Range(0, 10)
            .Select(_ => new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Movies, IsActive = true })
            .ToList();
        var tags = ids.ToDictionary(m => m.Id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "action" });
        var block = new ScheduleBlockEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RuleType = ScheduleBlockRuleType.TagPool,
            TagFilterCsv = "action",
            TypeFilterCsv = "Movies"
        };
        var ctx = new SchedulePlannerService.ResolveContext(ids, tags, new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc));

        var first = SchedulePlannerService.ResolveBlock(block, ctx);
        var second = SchedulePlannerService.ResolveBlock(block, ctx);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResolveBlock_TagPool_NoMatches_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            Id = Guid.NewGuid(),
            RuleType = ScheduleBlockRuleType.TagPool,
            TagFilterCsv = "horror"
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            Array.Empty<ChannelMediaEntity>(),
            new Dictionary<Guid, HashSet<string>>(),
            DateTime.UtcNow);

        Assert.Null(SchedulePlannerService.ResolveBlock(block, ctx));
    }
}
