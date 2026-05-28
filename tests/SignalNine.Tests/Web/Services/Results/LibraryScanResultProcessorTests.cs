using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services;
using SignalNine.Web.Services.Results;

namespace SignalNine.Tests.Web.Services.Results;

public sealed class LibraryScanResultProcessorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _libRoot;

    public LibraryScanResultProcessorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"signalnine-scan-proc-{Guid.NewGuid():N}");
        _libRoot = Path.Combine(Path.GetTempPath(), $"signalnine-libroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_libRoot);
    }

    private LibraryScanResultProcessor BuildProcessor(
        LibStubDataAccess<ChannelMediaEntity> media,
        LibStubDataAccess<MediaLibraryEntity> libraries,
        LibStubJobManager jobs)
    {
        var config = new SignalNineConfig
        {
            WorkSpace = new WorkSpaceConfig { Path = _tempRoot }
        };
        var stager = new WorkSpaceStager(config);
        var scopeFactory = new LibScanScopeFactory(media, libraries, jobs);
        return new LibraryScanResultProcessor(scopeFactory, stager);
    }

    private string CreateLibraryFile(string filename)
    {
        var path = Path.Combine(_libRoot, filename);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return filename; // return relative ref
    }

    private static string Serialize(LibraryScanResult result)
    {
        return JsonSerializer.Serialize(result);
    }

    // -------------------------------------------------------------------------
    // Test 1: Happy path — 3 LocalFile items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_HappyPath_ThreeLocalFileItems_InsertsAndEnqueues()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();

        var libId = Guid.NewGuid();
        var library = new MediaLibraryEntity
        {
            Id = libId,
            Name = "Test Library",
            SourceType = MediaSourceType.LocalFile,
            SourceRef = _libRoot,
            DefaultMediaType = ChannelMediaType.Movies,
            IsActive = true
        };
        libraries.Insert(library);

        var files = new[] { "movie1.mp4", "movie2.mp4", "movie3.mp4" };
        foreach (var f in files) CreateLibraryFile(f);

        var items = files.Select(f => new ScannedItem(
            Title: f,
            SourceRef: f,
            SourceType: (int)MediaSourceType.LocalFile,
            DurationSeconds: 3600,
            MovieReleaseYear: 2024,
            MovieDirector: "Director",
            TvSeriesName: null,
            TvSeason: null,
            TvEpisode: null
        )).ToList();

        var result = new LibraryScanResult(libId, items);
        var processor = BuildProcessor(media, libraries, jobs);

        var before = DateTime.UtcNow.AddSeconds(-1);
        await processor.ApplyAsync(Guid.NewGuid(), Serialize(result), CancellationToken.None);

        Assert.Equal(3, media.AllInserted.Count);
        Assert.All(media.AllInserted, m => Assert.Equal(libId, m.MediaLibraryId));
        Assert.All(media.AllInserted, m => Assert.Equal(MediaSourceType.LocalFile, m.SourceType));
        Assert.All(media.AllInserted, m => Assert.Equal(ChannelMediaType.Movies, m.Type));

        Assert.Equal(3, jobs.AllEnqueued.Count);
        Assert.All(jobs.AllEnqueued, c => Assert.Equal("media.pipeline", c.Type));

        foreach (var enqueued in jobs.AllEnqueued)
        {
            var insertedIds = media.AllInserted.Select(m => m.Id).ToHashSet();
            var payload = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(enqueued.PayloadJson);
            Assert.NotNull(payload);
            var channelMediaId = Guid.Parse(payload!["ChannelMediaId"]!.ToString());
            Assert.Contains(channelMediaId, insertedIds);
        }

        var updatedLib = libraries.AllUpdated.Last();
        Assert.NotNull(updatedLib.LastScannedAt);
        Assert.True(updatedLib.LastScannedAt >= before);
        Assert.True(updatedLib.UpdatedAt >= before);
    }

    // -------------------------------------------------------------------------
    // Test 2: Null result JSON → no-op
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_NullResultJson_DoesNothing()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();
        var processor = BuildProcessor(media, libraries, jobs);

        await processor.ApplyAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.Empty(media.AllInserted);
        Assert.Empty(jobs.AllEnqueued);
    }

    // -------------------------------------------------------------------------
    // Test 3: Whitespace result JSON → no-op
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WhitespaceResultJson_DoesNothing()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();
        var processor = BuildProcessor(media, libraries, jobs);

        await processor.ApplyAsync(Guid.NewGuid(), "   ", CancellationToken.None);

        Assert.Empty(media.AllInserted);
        Assert.Empty(jobs.AllEnqueued);
    }

    // -------------------------------------------------------------------------
    // Test 4: Library not found → throws InvalidOperationException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_LibraryNotFound_ThrowsInvalidOperationException()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();
        var processor = BuildProcessor(media, libraries, jobs);

        var missingId = Guid.NewGuid();
        var result = new LibraryScanResult(missingId, Array.Empty<ScannedItem>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ApplyAsync(Guid.NewGuid(), Serialize(result), CancellationToken.None)
        );
    }

    // -------------------------------------------------------------------------
    // Test 5: Empty items list — no inserts, no enqueues, library updated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_EmptyItems_NoInsertsNoEnqueues_LibraryTimestampsUpdated()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();

        var libId = Guid.NewGuid();
        libraries.Insert(new MediaLibraryEntity
        {
            Id = libId,
            Name = "Empty Library",
            SourceType = MediaSourceType.LocalFile,
            SourceRef = _libRoot,
            DefaultMediaType = ChannelMediaType.Movies,
            IsActive = true,
            LastScannedAt = null
        });

        var result = new LibraryScanResult(libId, Array.Empty<ScannedItem>());
        var processor = BuildProcessor(media, libraries, jobs);

        var before = DateTime.UtcNow.AddSeconds(-1);
        await processor.ApplyAsync(Guid.NewGuid(), Serialize(result), CancellationToken.None);

        Assert.Empty(media.AllInserted);
        Assert.Empty(jobs.AllEnqueued);

        Assert.Single(libraries.AllUpdated);
        var updatedLib = libraries.AllUpdated[0];
        Assert.NotNull(updatedLib.LastScannedAt);
        Assert.True(updatedLib.LastScannedAt >= before);
    }

    // -------------------------------------------------------------------------
    // Test 6: De-dup of existing rows — skips the duplicate, only 2 inserts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_DuplicateItem_SkipsExisting_InsertsOnlyNew()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();

        var libId = Guid.NewGuid();
        libraries.Insert(new MediaLibraryEntity
        {
            Id = libId,
            Name = "Dup Library",
            SourceType = MediaSourceType.LocalFile,
            SourceRef = _libRoot,
            DefaultMediaType = ChannelMediaType.Movies,
            IsActive = true
        });

        // Pre-seed one existing entity, then clear tracking so only processor inserts count
        media.Insert(new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            MediaLibraryId = libId,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "existing.mp4",
            Title = "Existing Movie"
        });
        media.ClearTracking();

        var files = new[] { "existing.mp4", "new1.mp4", "new2.mp4" };
        foreach (var f in files) CreateLibraryFile(f);

        var items = files.Select(f => new ScannedItem(
            Title: f,
            SourceRef: f,
            SourceType: (int)MediaSourceType.LocalFile,
            DurationSeconds: null,
            MovieReleaseYear: null,
            MovieDirector: null,
            TvSeriesName: null,
            TvSeason: null,
            TvEpisode: null
        )).ToList();

        var result = new LibraryScanResult(libId, items);
        var processor = BuildProcessor(media, libraries, jobs);

        await processor.ApplyAsync(Guid.NewGuid(), Serialize(result), CancellationToken.None);

        // Only 2 new inserts (existing.mp4 skipped)
        Assert.Equal(2, media.AllInserted.Count);
        Assert.DoesNotContain(media.AllInserted, m => m.SourceRef == "existing.mp4");

        Assert.Equal(2, jobs.AllEnqueued.Count);
    }

    // -------------------------------------------------------------------------
    // Test 7: Jellyfin items — INSERTs but NO pipeline enqueues
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_JellyfinItems_InsertsButSkipsPipelineEnqueue()
    {
        var media = new LibStubDataAccess<ChannelMediaEntity>();
        var libraries = new LibStubDataAccess<MediaLibraryEntity>();
        var jobs = new LibStubJobManager();

        var libId = Guid.NewGuid();
        libraries.Insert(new MediaLibraryEntity
        {
            Id = libId,
            Name = "Jellyfin Library",
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "http://jellyfin:8096",
            DefaultMediaType = ChannelMediaType.Movies,
            IsActive = true
        });

        var items = new List<ScannedItem>
        {
            new ScannedItem(
                Title: "Jellyfin Movie 1",
                SourceRef: "jf-item-001",
                SourceType: (int)MediaSourceType.Jellyfin,
                DurationSeconds: 5400,
                MovieReleaseYear: null,
                MovieDirector: null,
                TvSeriesName: null,
                TvSeason: null,
                TvEpisode: null
            ),
            new ScannedItem(
                Title: "Jellyfin Movie 2",
                SourceRef: "jf-item-002",
                SourceType: (int)MediaSourceType.Jellyfin,
                DurationSeconds: 4200,
                MovieReleaseYear: null,
                MovieDirector: null,
                TvSeriesName: null,
                TvSeason: null,
                TvEpisode: null
            )
        };

        var result = new LibraryScanResult(libId, items);
        var processor = BuildProcessor(media, libraries, jobs);

        await processor.ApplyAsync(Guid.NewGuid(), Serialize(result), CancellationToken.None);

        Assert.Equal(2, media.AllInserted.Count);
        Assert.All(media.AllInserted, m => Assert.Equal(MediaSourceType.Jellyfin, m.SourceType));

        // No pipeline enqueues — Jellyfin stager returns empty RelativeInputFile
        Assert.Empty(jobs.AllEnqueued);
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { /* best effort */ }

        try
        {
            if (Directory.Exists(_libRoot))
                Directory.Delete(_libRoot, recursive: true);
        }
        catch { /* best effort */ }
    }
}

// -------------------------------------------------------------------------
// Stubs — internal to this test file
// -------------------------------------------------------------------------

internal sealed class LibStubDataAccess<TEntity> : IDataAccess<TEntity> where TEntity : class
{
    private readonly Dictionary<object, TEntity> _store = new();
    private readonly List<TEntity> _inserted = new();
    private readonly List<TEntity> _updated = new();

    public IReadOnlyList<TEntity> AllInserted => _inserted;
    public IReadOnlyList<TEntity> AllUpdated => _updated;

    public void ClearTracking()
    {
        _inserted.Clear();
        _updated.Clear();
    }

    public TEntity? GetByKey(object key)
    {
        return _store.TryGetValue(key, out var entity) ? entity : null;
    }

    public IReadOnlyList<TEntity> List()
    {
        return _store.Values.ToList();
    }

    public TEntity Insert(TEntity entity)
    {
        var key = GetPrimaryKey(entity);
        _store[key] = entity;
        _inserted.Add(entity);
        return entity;
    }

    public int Update(TEntity entity)
    {
        var key = GetPrimaryKey(entity);
        _store[key] = entity;
        _updated.Add(entity);
        return 1;
    }

    public int Delete(object key)
    {
        return _store.Remove(key) ? 1 : 0;
    }

    private static object GetPrimaryKey(TEntity entity)
    {
        var prop = typeof(TEntity).GetProperty("Id")
                   ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} has no Id property.");
        return prop.GetValue(entity) ?? throw new InvalidOperationException("Id value is null.");
    }
}

internal sealed class LibStubJobManager : IJobManager
{
    private readonly List<EnqueueJobCommand> _enqueued = new();

    public IReadOnlyList<EnqueueJobCommand> AllEnqueued => _enqueued;

    public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
    {
        _enqueued.Add(command);
        var snapshot = new JobSnapshot
        {
            Id = Guid.NewGuid(),
            Type = command.Type,
            State = JobStateType.Queued,
            Progress = new JobProgressSnapshot { Percent = 0, Message = "queued" },
            CreatedAt = DateTime.UtcNow
        };
        return Task.FromResult(snapshot);
    }

    public IReadOnlyList<JobSnapshot> List() => Array.Empty<JobSnapshot>();
    public JobSnapshot? GetById(Guid jobId) => null;
    public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId) => Array.Empty<JobLogEntry>();
    public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return Guid.Empty;
    }

    public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
        => DequeueAsync(cancellationToken);

    public Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public CancellationToken GetCancellationToken(Guid jobId) => CancellationToken.None;
}

internal sealed class LibScanScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;

    public LibScanScopeFactory(
        IDataAccess<ChannelMediaEntity> media,
        IDataAccess<MediaLibraryEntity> libraries,
        IJobManager jobs)
    {
        _provider = new LibScanServiceProvider(media, libraries, jobs);
    }

    public IServiceScope CreateScope()
    {
        return new LibScanServiceScope(_provider);
    }
}

internal sealed class LibScanServiceScope : IServiceScope
{
    private readonly IServiceProvider _provider;

    public LibScanServiceScope(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IServiceProvider ServiceProvider => _provider;

    public void Dispose()
    {
    }
}

internal sealed class LibScanServiceProvider : IServiceProvider
{
    private readonly IDataAccess<ChannelMediaEntity> _media;
    private readonly IDataAccess<MediaLibraryEntity> _libraries;
    private readonly IJobManager _jobs;

    public LibScanServiceProvider(
        IDataAccess<ChannelMediaEntity> media,
        IDataAccess<MediaLibraryEntity> libraries,
        IJobManager jobs)
    {
        _media = media;
        _libraries = libraries;
        _jobs = jobs;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IDataAccess<ChannelMediaEntity>)) return _media;
        if (serviceType == typeof(IDataAccess<MediaLibraryEntity>)) return _libraries;
        if (serviceType == typeof(IJobManager)) return _jobs;
        return null;
    }
}
