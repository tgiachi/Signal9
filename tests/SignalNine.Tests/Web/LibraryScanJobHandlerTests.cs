using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Services;

namespace SignalNine.Tests.Web;

public class LibraryScanJobHandlerTests
{
    private static (LibraryScanJobHandler Handler, StubLibraryDataAccess Libraries, StubJellyfin Jellyfin, StubWalker Walker)
        Build()
    {
        var jellyfin = new StubJellyfin();
        var walker = new StubWalker();
        var libraries = new StubLibraryDataAccess();

        var services = new ServiceCollection();
        services.AddScoped<IJellyfinService>(_ => jellyfin);
        services.AddScoped<ILocalLibraryWalker>(_ => walker);
        services.AddScoped<IDataAccess<MediaLibraryEntity>>(_ => libraries);
        var sp = services.BuildServiceProvider();

        var handler = new LibraryScanJobHandler(sp.GetRequiredService<IServiceScopeFactory>());
        return (handler, libraries, jellyfin, walker);
    }

    private static JobExecutionContext NewContext(Guid mediaLibraryId)
    {
        var payload = JsonSerializer.Serialize(new ScanLibraryPayload(mediaLibraryId));
        var workDir = Path.Combine(Path.GetTempPath(), $"scan-test-{Guid.NewGuid():N}");
        return new JobExecutionContext(Guid.NewGuid(), payload, workDir, new InMemoryJobBus());
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
    public async Task EmptyJellyfinLibrary_ReturnsZeroItems()
    {
        var (handler, libraries, jellyfin, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>();

        var result = (LibraryScanResult)await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(lib.Id, result.LibraryId);
    }

    [Fact]
    public async Task JellyfinMovies_ReturnsNItems_WithCorrectFields()
    {
        var (handler, libraries, jellyfin, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("jf-1", "Die Hard", "Movie", 7320L * 10_000_000, 1988, null, null, null, null),
            new("jf-2", "Aliens", "Movie", 8400L * 10_000_000, 1986, null, null, null, null),
            new("jf-3", "Heat", "Movie", 10200L * 10_000_000, 1995, null, null, null, null)
        };

        var result = (LibraryScanResult)await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(lib.Id, result.LibraryId);

        var dieHard = result.Items.Single(i => i.SourceRef == "jf-1");
        Assert.Equal("Die Hard", dieHard.Title);
        Assert.Equal(7320, dieHard.DurationSeconds);
        Assert.Equal(1988, dieHard.MovieReleaseYear);
        Assert.Equal((int)MediaSourceType.Jellyfin, dieHard.SourceType);
        Assert.All(result.Items, i => Assert.Equal((int)MediaSourceType.Jellyfin, i.SourceType));
    }

    [Fact]
    public async Task JellyfinTvShow_MapsSeriesSeasonEpisode()
    {
        var (handler, libraries, jellyfin, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.TvShow);
        libraries.Add(lib);
        jellyfin.Items["jf-lib-1"] = new List<JellyfinItem>
        {
            new("ep-1", "Pilot", "Episode", 2700L * 10_000_000, null, null, "Breaking Bad", 1, 1)
        };

        var result = (LibraryScanResult)await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Breaking Bad", item.TvSeriesName);
        Assert.Equal(1, item.TvSeason);
        Assert.Equal(1, item.TvEpisode);
        Assert.Null(item.MovieReleaseYear);
    }

    [Fact]
    public async Task LocalFile_ReturnsNItems_WithCorrectFields()
    {
        var (handler, libraries, _, walker) = Build();
        var lib = NewLocalLibrary(ChannelMediaType.Movies);
        libraries.Add(lib);
        walker.Items["/media/movies"] = new List<LocalLibraryItem>
        {
            new("a.mp4", "a", 100),
            new("sub/b.mkv", "b", 200),
            new("sub/c.mov", "c", 300),
            new("d.avi", "d", 400)
        };

        var result = (LibraryScanResult)await handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None);

        Assert.Equal(4, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal((int)MediaSourceType.LocalFile, i.SourceType));
        Assert.All(result.Items, i => Assert.Null(i.DurationSeconds));

        var b = result.Items.Single(i => i.SourceRef == "sub/b.mkv");
        Assert.Equal("b", b.Title);
    }

    [Fact]
    public async Task InactiveLibrary_ThrowsInvalidOperationException()
    {
        var (handler, libraries, _, _) = Build();
        var lib = NewJellyfinLibrary(ChannelMediaType.Movies, active: false);
        libraries.Add(lib);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecuteAsync(NewContext(lib.Id), CancellationToken.None)
        );
    }

    [Fact]
    public async Task MissingLibrary_Throws()
    {
        var (handler, _, _, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecuteAsync(NewContext(Guid.NewGuid()), CancellationToken.None)
        );
    }

    [Fact]
    public async Task UrlSource_ThrowsNotSupportedException()
    {
        var (handler, libraries, _, _) = Build();
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
    public async Task Cancellation_ThrowsOperationCanceledException()
    {
        var (handler, libraries, jellyfin, _) = Build();
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

        public Task<JellyfinItemTags> GetItemTagsAsync(string itemId, CancellationToken ct = default)
        {
            return Task.FromResult(new JellyfinItemTags(Array.Empty<string>(), Array.Empty<string>()));
        }

        public Task<IReadOnlyList<JellyfinPreviewImage>> GetPreviewImagesAsync(
            string itemId,
            int maxImages,
            CancellationToken ct = default
        )
        {
            return Task.FromResult<IReadOnlyList<JellyfinPreviewImage>>(Array.Empty<JellyfinPreviewImage>());
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
}
