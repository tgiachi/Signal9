using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Services.Results;

namespace SignalNine.Tests.Web.Services.Results;

public sealed class MediaPipelineResultProcessorTests : IDisposable
{
    private readonly string _tempRoot;

    public MediaPipelineResultProcessorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"signalnine-pipeline-proc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    private MediaPipelineResultProcessor BuildProcessor(
        StubDataAccess<ChannelMediaEntity> media,
        StubAssetStore assets,
        bool cleanup = true)
    {
        var config = new SignalNineConfig
        {
            WorkSpace = new WorkSpaceConfig
            {
                Path = _tempRoot,
                CleanupAfterProcessing = cleanup
            }
        };
        var scopeFactory = new StubScopeFactory(media);
        return new MediaPipelineResultProcessor(scopeFactory, assets, config);
    }

    private string CreateOutputDir(Guid jobId, params string[] filenames)
    {
        var outputDir = Path.Combine(_tempRoot, "jobs", jobId.ToString(), "output");
        Directory.CreateDirectory(outputDir);
        foreach (var fn in filenames)
        {
            File.WriteAllBytes(Path.Combine(outputDir, fn), Array.Empty<byte>());
        }
        return outputDir;
    }

    private static string Serialize(MediaPipelineResult result)
    {
        return JsonSerializer.Serialize(result);
    }

    // -------------------------------------------------------------------------
    // Test 1: Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_HappyPath_UpdatesMediaUploadsPreviews_AndDeletesWorkDir()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets, cleanup: true);
        var mediaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        media.Insert(new ChannelMediaEntity { Id = mediaId, DurationSeconds = null });
        CreateOutputDir(jobId, "thumb-001.jpg", "thumb-002.jpg");

        var result = new MediaPipelineResult(
            ChannelMediaId: mediaId,
            DurationSeconds: 7320,
            PreviewFiles: new[] { "thumb-001.jpg", "thumb-002.jpg" },
            ProbeJson: null
        );

        await processor.ApplyAsync(jobId, Serialize(result), CancellationToken.None);

        var updated = media.GetByKey(mediaId);
        Assert.NotNull(updated);
        Assert.Equal(7320, updated.DurationSeconds);

        Assert.Equal(2, assets.PutCalls.Count);
        Assert.Contains(assets.PutCalls, c => c.Filename == "thumb-001.jpg" && c.MediaId == mediaId);
        Assert.Contains(assets.PutCalls, c => c.Filename == "thumb-002.jpg" && c.MediaId == mediaId);

        var workDir = Path.Combine(_tempRoot, "jobs", jobId.ToString());
        Assert.False(Directory.Exists(workDir));
    }

    // -------------------------------------------------------------------------
    // Test 2: Null result JSON — no-op
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_NullResultJson_DoesNothing()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets);
        var jobId = Guid.NewGuid();

        await processor.ApplyAsync(jobId, null, CancellationToken.None);

        Assert.Empty(media.AllUpdated);
        Assert.Empty(assets.PutCalls);
    }

    // -------------------------------------------------------------------------
    // Test 3: Whitespace result JSON — no-op
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WhitespaceResultJson_DoesNothing()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets);
        var jobId = Guid.NewGuid();

        await processor.ApplyAsync(jobId, "   ", CancellationToken.None);

        Assert.Empty(media.AllUpdated);
        Assert.Empty(assets.PutCalls);
    }

    // -------------------------------------------------------------------------
    // Test 4: Missing channel media in DB — previews still uploaded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_MissingChannelMedia_SkipsDbUpdate_StillUploadsPreview()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets, cleanup: false);
        var mediaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        // No entity inserted — GetByKey returns null
        CreateOutputDir(jobId, "thumb-001.jpg");

        var result = new MediaPipelineResult(
            ChannelMediaId: mediaId,
            DurationSeconds: 120,
            PreviewFiles: new[] { "thumb-001.jpg" },
            ProbeJson: null
        );

        var ex = await Record.ExceptionAsync(() => processor.ApplyAsync(jobId, Serialize(result), CancellationToken.None));

        Assert.Null(ex);
        Assert.Empty(media.AllUpdated);
        Assert.Single(assets.PutCalls);
        Assert.Equal("thumb-001.jpg", assets.PutCalls[0].Filename);
    }

    // -------------------------------------------------------------------------
    // Test 5: Null DurationSeconds — DB update skipped, previews still copied
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_NullDurationSeconds_SkipsDbUpdate_StillUploadsPreview()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets, cleanup: false);
        var mediaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        media.Insert(new ChannelMediaEntity { Id = mediaId, DurationSeconds = 999 });
        CreateOutputDir(jobId, "thumb-001.jpg");

        var result = new MediaPipelineResult(
            ChannelMediaId: mediaId,
            DurationSeconds: null,
            PreviewFiles: new[] { "thumb-001.jpg" },
            ProbeJson: null
        );

        await processor.ApplyAsync(jobId, Serialize(result), CancellationToken.None);

        Assert.Empty(media.AllUpdated);
        Assert.Single(assets.PutCalls);
    }

    // -------------------------------------------------------------------------
    // Test 6: Missing preview source file — skipped without throw, rest uploaded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_MissingPreviewFile_SkipsFile_UploadsExisting()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets, cleanup: false);
        var mediaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        media.Insert(new ChannelMediaEntity { Id = mediaId });
        CreateOutputDir(jobId, "thumb-001.jpg"); // thumb-002.jpg intentionally absent

        var result = new MediaPipelineResult(
            ChannelMediaId: mediaId,
            DurationSeconds: null,
            PreviewFiles: new[] { "thumb-001.jpg", "thumb-002.jpg" },
            ProbeJson: null
        );

        var ex = await Record.ExceptionAsync(() => processor.ApplyAsync(jobId, Serialize(result), CancellationToken.None));

        Assert.Null(ex);
        Assert.Single(assets.PutCalls);
        Assert.Equal("thumb-001.jpg", assets.PutCalls[0].Filename);
    }

    // -------------------------------------------------------------------------
    // Test 7: CleanupAfterProcessing = false — workdir NOT deleted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_CleanupDisabled_WorkDirNotDeleted()
    {
        var media = new StubDataAccess<ChannelMediaEntity>();
        var assets = new StubAssetStore();
        var processor = BuildProcessor(media, assets, cleanup: false);
        var mediaId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        media.Insert(new ChannelMediaEntity { Id = mediaId });
        CreateOutputDir(jobId, "thumb-001.jpg");

        var result = new MediaPipelineResult(
            ChannelMediaId: mediaId,
            DurationSeconds: 60,
            PreviewFiles: new[] { "thumb-001.jpg" },
            ProbeJson: null
        );

        await processor.ApplyAsync(jobId, Serialize(result), CancellationToken.None);

        var workDir = Path.Combine(_tempRoot, "jobs", jobId.ToString());
        Assert.True(Directory.Exists(workDir));
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}

// -------------------------------------------------------------------------
// Stubs — internal to this namespace
// -------------------------------------------------------------------------

internal sealed class StubDataAccess<TEntity> : IDataAccess<TEntity> where TEntity : class
{
    private readonly Dictionary<object, TEntity> _store = new();
    private readonly List<TEntity> _updated = new();

    public IReadOnlyList<TEntity> AllUpdated => _updated;

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

internal sealed class StubAssetStore : IAssetStore
{
    public sealed record PutCall(Guid MediaId, string Filename, string SourcePath);

    private readonly List<PutCall> _putCalls = new();

    public IReadOnlyList<PutCall> PutCalls => _putCalls;

    public Task PutPreviewAsync(Guid mediaId, string filename, string sourcePath, CancellationToken cancellationToken = default)
    {
        _putCalls.Add(new PutCall(mediaId, filename, sourcePath));
        return Task.CompletedTask;
    }

    public Task DeletePreviewsAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GetPreviewUrl(Guid mediaId, int index)
    {
        return string.Empty;
    }
}

internal sealed class StubScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;

    public StubScopeFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public StubScopeFactory(IDataAccess<ChannelMediaEntity> media)
    {
        _provider = new StubServiceProvider(media);
    }

    public IServiceScope CreateScope()
    {
        return new StubServiceScope(_provider);
    }
}

internal sealed class StubServiceScope : IServiceScope, IAsyncDisposable
{
    private readonly IServiceProvider _provider;

    public StubServiceScope(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IServiceProvider ServiceProvider => _provider;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class StubServiceProvider : IServiceProvider
{
    private readonly IDataAccess<ChannelMediaEntity> _media;

    public StubServiceProvider(IDataAccess<ChannelMediaEntity> media)
    {
        _media = media;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IDataAccess<ChannelMediaEntity>))
        {
            return _media;
        }
        return null;
    }
}
