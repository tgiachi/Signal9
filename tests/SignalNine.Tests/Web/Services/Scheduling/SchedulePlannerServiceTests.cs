using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
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

    [Fact]
    public void ResolveFallback_UsesChannelFallbackTagAndType()
    {
        var match = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Movies, IsActive = true };
        var skip = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Commercial, IsActive = true };
        var channel = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            FallbackTagFilterCsv = null,
            FallbackTypeFilterCsv = "Movies"
        };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { match, skip },
            new Dictionary<Guid, HashSet<string>>(),
            new DateTime(2026, 6, 1, 23, 0, 0, DateTimeKind.Utc));

        Assert.Equal(match.Id, SchedulePlannerService.ResolveFallback(channel, ctx));
    }

    [Fact]
    public void ResolveFallback_DefaultsToMoviesAndTvShowWhenNoFilter()
    {
        var movie = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Movies, IsActive = true };
        var ad = new ChannelMediaEntity { Id = Guid.NewGuid(), Type = ChannelMediaType.Commercial, IsActive = true };
        var channel = new ChannelEntity { Id = Guid.NewGuid() };
        var ctx = new SchedulePlannerService.ResolveContext(
            new[] { movie, ad },
            new Dictionary<Guid, HashSet<string>>(),
            new DateTime(2026, 6, 1, 23, 0, 0, DateTimeKind.Utc));

        var picked = SchedulePlannerService.ResolveFallback(channel, ctx);
        Assert.Equal(movie.Id, picked);
    }

    [Fact]
    public void EmitMediaWithBreaks_NoBreaks_SingleEntry()
    {
        var channel = new ChannelEntity
        {
            CommercialsEnabled = false,
            CommercialIntervalMinSeconds = 60
        };
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            DurationSeconds = 30,
            Type = ChannelMediaType.Movies,
            IsActive = true
        };
        var start = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var sink = new List<ScheduledEntryEntity>();

        var rng = new Random(42);
        var elapsed = SchedulePlannerService.EmitMediaWithBreaks(
            channel,
            media,
            start,
            maxDurationSeconds: 30,
            sourceBlockId: null,
            adsPool: Array.Empty<ChannelMediaEntity>(),
            bumpersPool: Array.Empty<ChannelMediaEntity>(),
            rng,
            sink);

        Assert.Equal(30, elapsed);
        Assert.Single(sink);
        Assert.Equal(ScheduledEntryKind.Media, sink[0].Kind);
        Assert.Equal(30, sink[0].DurationSeconds);
        Assert.Equal(0, sink[0].MediaPartIndex);
        Assert.Equal(1, sink[0].MediaPartCount);
        Assert.Equal(media.Id, sink[0].ChannelMediaId);
    }

    [Fact]
    public void EmitMediaWithBreaks_OneBreak_SplitsMediaAndInsertsAdsAndBumpers()
    {
        var channel = new ChannelEntity
        {
            CommercialsEnabled = true,
            CommercialBumpersEnabled = true,
            CommercialBreakSize = 2,
            CommercialIntervalMinSeconds = 600,    // 10 minutes
            CommercialIntervalJitterSeconds = 0    // deterministic for the test
        };
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            DurationSeconds = 1200,                // 20 minutes → exactly 1 break expected
            Type = ChannelMediaType.Movies,
            IsActive = true
        };
        var ad1 = new ChannelMediaEntity { Id = Guid.NewGuid(), DurationSeconds = 30, Type = ChannelMediaType.Commercial };
        var ad2 = new ChannelMediaEntity { Id = Guid.NewGuid(), DurationSeconds = 30, Type = ChannelMediaType.Commercial };
        var bumper = new ChannelMediaEntity { Id = Guid.NewGuid(), DurationSeconds = 8, Type = ChannelMediaType.Bumper };
        var start = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var sink = new List<ScheduledEntryEntity>();

        var rng = new Random(123);
        var elapsed = SchedulePlannerService.EmitMediaWithBreaks(
            channel,
            media,
            start,
            maxDurationSeconds: 1200,
            sourceBlockId: null,
            adsPool: new[] { ad1, ad2 },
            bumpersPool: new[] { bumper },
            rng,
            sink);

        // 600s media + 8s bumper + 30s + 30s + 8s bumper + 600s media = 1276s
        Assert.Equal(1276, elapsed);
        Assert.Equal(6, sink.Count);
        Assert.Equal(ScheduledEntryKind.Media, sink[0].Kind);
        Assert.Equal(600, sink[0].DurationSeconds);
        Assert.Equal(ScheduledEntryKind.Bumper, sink[1].Kind);
        Assert.Equal(ScheduledEntryKind.Commercial, sink[2].Kind);
        Assert.Equal(ScheduledEntryKind.Commercial, sink[3].Kind);
        Assert.Equal(ScheduledEntryKind.Bumper, sink[4].Kind);
        Assert.Equal(ScheduledEntryKind.Media, sink[5].Kind);
        Assert.Equal(600, sink[5].DurationSeconds);
        Assert.Equal(1, sink[5].MediaPartIndex);
        Assert.Equal(600, sink[5].MediaOffsetSeconds);
    }

    [Fact]
    public async Task PlanChannelAsync_OneBlock_EmitsEntriesInRange()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var rootDir = Path.Combine(Path.GetTempPath(), $"schedule-test-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("SIGNAL9_ROOT_DIRECTORY", rootDir);
        try
        {
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var channels = sp.GetRequiredService<IDataAccess<ChannelEntity>>();
            var media = sp.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
            var blocks = sp.GetRequiredService<IDataAccess<ScheduleBlockEntity>>();
            var entries = sp.GetRequiredService<IDataAccess<ScheduledEntryEntity>>();
            var planner = sp.GetRequiredService<SchedulePlannerService>();

            var channel = new ChannelEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Slug = "test",
                IsActive = true,
                CommercialsEnabled = false
            };
            channels.Insert(channel);

            var movie = new ChannelMediaEntity
            {
                Id = Guid.NewGuid(),
                Title = "Inception",
                Type = ChannelMediaType.Movies,
                DurationSeconds = 7200,
                IsActive = true
            };
            media.Insert(movie);

            var block = new ScheduleBlockEntity
            {
                Id = Guid.NewGuid(),
                ChannelId = channel.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(20, 0, 0),
                DurationMinutes = 120,
                RuleType = ScheduleBlockRuleType.Pin,
                PinnedChannelMediaId = movie.Id,
                IsActive = true
            };
            blocks.Insert(block);

            var monday = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var written = await planner.PlanChannelAsync(channel.Id, monday, monday.AddDays(1));

            Assert.True(written >= 1);
            var emitted = entries.List().Where(e => e.ChannelId == channel.Id).OrderBy(e => e.StartAt).ToList();
            Assert.Contains(emitted, e => e.ChannelMediaId == movie.Id && e.Kind == ScheduledEntryKind.Media);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNAL9_ROOT_DIRECTORY", null);
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, recursive: true);
        }
    }
}
