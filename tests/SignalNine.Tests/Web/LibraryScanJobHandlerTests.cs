using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web;

public class LibraryScanJobHandlerTests
{
    private static (LibraryScanJobHandler Handler, StubMediaDataAccess Media, StubLibraryDataAccess Libraries, StubJellyfin Jellyfin, StubWalker Walker, StubJobManager Jobs)
        Build()
    {
        var jellyfin = new StubJellyfin();
        var walker = new StubWalker();
        var libraries = new StubLibraryDataAccess();
        var media = new StubMediaDataAccess();
        var jobs = new StubJobManager();

        var services = new ServiceCollection();
        services.AddScoped<IJellyfinService>(_ => jellyfin);
        services.AddScoped<ILocalLibraryWalker>(_ => walker);
        services.AddScoped<IDataAccess<MediaLibraryEntity>>(_ => libraries);
        services.AddScoped<IDataAccess<ChannelMediaEntity>>(_ => media);
        services.AddScoped<IJobManager>(_ => jobs);
        var sp = services.BuildServiceProvider();

        var handler = new LibraryScanJobHandler(sp.GetRequiredService<IServiceScopeFactory>());
        return (handler, media, libraries, jellyfin, walker, jobs);
    }

    private static JobExecutionContext NewContext(Guid mediaLibraryId)
    {
        var payload = JsonSerializer.Serialize(new ScanLibraryPayload(mediaLibraryId));
        return new JobExecutionContext(Guid.NewGuid(), payload, new StubJobManager());
    }

    private static MediaLibraryEntity NewJellyfinLibrary(ChannelMediaType type, bool active = true)
    {
        return new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Lib",
            DefaultMediaType = type,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-lib-1",
            IsActive = active
        };
    }

    private static MediaLibraryEntity NewLocalLibrary(ChannelMediaType type, bool active = true)
    {
        return new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = "LocalLib",
            DefaultMediaType = type,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "/media/movies",
            IsActive = active
        };
    }

    [Fact]
    public async Task FirstScan_JellyfinMovies_Inserts3Rows()
    {
        var (handler, media, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null),
            new("jf-2", "Aliens", "Movie", 8400L * 10_000_000, 1986, null, null, null, null),
            new("jf-3", "Heat", "Movie", 10200L * 10_000_000, 1995, null, null, null, null)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        var rows = media.List();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(lib.Id, r.MediaLibraryId));
        Assert.All(rows, r => Assert.Equal(ChannelMediaType.Movies, r.Type));
        Assert.All(rows, r => Assert.Equal(MediaSourceType.Jellyfin, r.SourceType));
        var dieHard = rows.Single(r => r.SourceRef == "jf-1");
        Assert.Equal("Die Hard", dieHard.Title);
        Assert.Equal(7320, dieHard.DurationSeconds);
        Assert.Equal(1988, dieHard.MovieReleaseYear);
    }

    [Fact]
    public async Task ReScan_Jellyfin_UpsertsWithoutDuplicates()
    {
        var (handler, media, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);
        Assert.Single(media.List());

        jellyfin.Items["jf-lib-1"][0] = new JellyfinItem(
            "jf-1", "Die Hard (Remastered)", "Movie", 7320L * 10_000_000, 1988, null, null, null, null);

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        var rows = media.List();
        Assert.Single(rows);
        Assert.Equal("Die Hard (Remastered)", rows[0].Title);
    }

    [Fact]
    public async Task ReScan_AfterItemRemoved_LeavesOrphan()
    {
        var (handler, media, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null),
            new("jf-2", "Aliens", "Movie", 8400L * 10_000_000, 1986, null, null, null, null)
        };
        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);
        Assert.Equal(2, media.List().Count);

        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null)
        };
        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Equal(2, media.List().Count);
    }

    [Fact]
    public async Task JellyfinTvShow_MapsSeriesSeasonEpisode()
    {
        var (handler, media, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.TvShow);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("ep-1", "Pilot", "Episode", 2700L * 10_000_000, null, null, "Breaking Bad", 1, 1)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        var row = media.List().Single();
        Assert.Equal(ChannelMediaType.TvShow, row.Type);
        Assert.Equal("Breaking Bad", row.TvSeriesName);
        Assert.Equal(1, row.TvSeason);
        Assert.Equal(1, row.TvEpisode);
    }

    [Fact]
    public async Task LocalFile_Library_Inserts4Rows()
    {
        var (handler, media, libraries, _, walker, _) = Build();
        var lib = NewLocalLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        walker.Items["/media/movies"] = new List<LocalLibraryItem>
        {
            new("a.mp4", "a", 100),
            new("sub/b.mkv", "b", 200),
            new("sub/c.mov", "c", 300),
            new("d.avi", "d", 400)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        var rows = media.List();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal(MediaSourceType.LocalFile, r.SourceType));
        Assert.All(rows, r => Assert.Null(r.DurationSeconds));
        Assert.Contains(rows, r => r.SourceRef == "sub/b.mkv" && r.Title == "b");
    }

    [Fact]
    public async Task LocalFile_ReScan_NoDuplicates()
    {
        var (handler, media, libraries, _, walker, _) = Build();
        var lib = NewLocalLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        walker.Items["/media/movies"] = new List<LocalLibraryItem>
        {
            new("a.mp4", "a", 100)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);
        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Single(media.List());
    }

    [Fact]
    public async Task LocalFile_RootMissing_Throws()
    {
        var (handler, _, libraries, _, walker, _) = Build();
        var lib = NewLocalLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        walker.ThrowDirectoryNotFound = true;

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None)
        );
    }

    [Fact]
    public async Task UrlSource_ThrowsNotSupported()
    {
        var (handler, _, libraries, _, _, _) = Build();
        var lib = new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Url",
            DefaultMediaType = ChannelMediaType.Movies,
            SourceType = MediaSourceType.Url,
            SourceRef = "https://example.com/",
            IsActive = true
        };
        libraries.Add(lib);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None)
        );
    }

    [Fact]
    public async Task InactiveLibrary_Throws()
    {
        var (handler, _, libraries, _, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies, active: false);
        libraries.Add(lib);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None)
        );
    }

    [Fact]
    public async Task MissingLibrary_Throws()
    {
        var (handler, _, _, _, _, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecuteAsync(NewContext(Guid.NewGuid()), CancellationToken.None)
        );
    }

    [Fact]
    public async Task LastScannedAt_UpdatedOnSuccess()
    {
        var (handler, _, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>();

        var before = lib.LastScannedAt;
        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.NotEqual(before, libraries.GetByKey(lib.Id)!.LastScannedAt);
        Assert.NotNull(libraries.GetByKey(lib.Id)!.LastScannedAt);
    }

    [Fact]
    public async Task Scan_NewItems_EnqueuesMediaPipelineJobs()
    {
        var (handler, _, libraries, jellyfin, _, jobs) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null),
            new("jf-2", "Aliens", "Movie", 8400L * 10_000_000, 1986, null, null, null, null)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Equal(2, jobs.Enqueued.Count);
        Assert.All(jobs.Enqueued, j => Assert.Equal("media.pipeline", j.Type));
    }

    [Fact]
    public async Task Scan_ExistingItems_DoNotEnqueuePipeline()
    {
        var (handler, _, libraries, jellyfin, _, jobs) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null)
        };

        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);
        Assert.Single(jobs.Enqueued);

        // Re-scan: the same item already exists, so no new pipeline job.
        jobs.Enqueued.Clear();
        await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Empty(jobs.Enqueued);
    }

    [Fact]
    public async Task Cancellation_AbortsScan()
    {
        var (handler, _, libraries, jellyfin, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = Enumerable.Range(0, 100)
            .Select(i => new JellyfinItem($"i-{i}", $"T{i}", "Movie", null, null, null, null, null, null))
            .ToList();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(NewContext(lib.Id), cts.Token)
        );
    }

    // ── Stubs ────────────────────────────────────────────────────────────

    private sealed class StubJellyfin : IJellyfinService
    {
        public Dictionary<string, List<JellyfinItem>> Items { get; } = new();

        public Task<JellyfinServerInfo> GetServerInfoAsync(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<JellyfinLibrarySummary>> ListLibrariesAsync(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<JellyfinItem?> GetItemAsync(string itemId, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<JellyfinItem>> ListItemsAsync(string libraryId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<JellyfinItem>>(
                Items.TryGetValue(libraryId, out var list) ? list : new List<JellyfinItem>()
            );
        }
    }

    private sealed class StubWalker : ILocalLibraryWalker
    {
        public Dictionary<string, List<LocalLibraryItem>> Items { get; } = new();
        public bool ThrowDirectoryNotFound { get; set; }

        public IEnumerable<LocalLibraryItem> Walk(string rootPath, CancellationToken ct)
        {
            if (ThrowDirectoryNotFound)
            {
                throw new DirectoryNotFoundException(rootPath);
            }
            if (!Items.TryGetValue(rootPath, out var list)) return Array.Empty<LocalLibraryItem>();
            return list;
        }
    }

    private sealed class StubLibraryDataAccess : IDataAccess<MediaLibraryEntity>
    {
        private readonly List<MediaLibraryEntity> _rows = new();
        public void Add(MediaLibraryEntity e)
        {
            _rows.Add(e);
        }
        public MediaLibraryEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(r => r.Id.Equals(key));
        }
        public IReadOnlyList<MediaLibraryEntity> List()
        {
            return _rows;
        }
        public MediaLibraryEntity Insert(MediaLibraryEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }
        public int Update(MediaLibraryEntity entity)
        {
            return 1;
        }
        public int Delete(object key)
        {
            return _rows.RemoveAll(r => r.Id.Equals(key));
        }
    }

    private sealed class StubMediaDataAccess : IDataAccess<ChannelMediaEntity>
    {
        private readonly List<ChannelMediaEntity> _rows = new();
        public ChannelMediaEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(r => r.Id.Equals(key));
        }
        public IReadOnlyList<ChannelMediaEntity> List()
        {
            return _rows;
        }
        public ChannelMediaEntity Insert(ChannelMediaEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }
        public int Update(ChannelMediaEntity entity)
        {
            return 1;
        }
        public int Delete(object key)
        {
            return _rows.RemoveAll(r => r.Id.Equals(key));
        }
    }

    private sealed class StubJobManager : IJobManager
    {
        public List<EnqueueJobCommand> Enqueued { get; } = new();

        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(command);
            return Task.FromResult(new JobSnapshot
            {
                Id = Guid.NewGuid(),
                Type = command.Type,
                PayloadJson = command.PayloadJson
            });
        }
        public IReadOnlyList<JobSnapshot> List()
        {
            return Array.Empty<JobSnapshot>();
        }
        public JobSnapshot? GetById(Guid jobId)
        {
            return null;
        }
        public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
        {
            return Array.Empty<JobLogEntry>();
        }
        public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
        public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
        public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public CancellationToken GetCancellationToken(Guid jobId)
        {
            return CancellationToken.None;
        }
    }
}
