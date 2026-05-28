using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class JellyfinPreviewTaskTests : IDisposable
{
    private readonly string _rootDir;

    public JellyfinPreviewTaskTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"signal9-jellyfin-preview-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        foreach (var name in Enum.GetNames<DirectoryType>())
        {
            Directory.CreateDirectory(Path.Combine(_rootDir, name));
        }
    }

    [Fact]
    public void Order_Is150()
    {
        var (task, _, _) = Build();

        Assert.Equal(150, task.Order);
    }

    [Fact]
    public async Task Execute_JellyfinMedia_WritesPreviewImages()
    {
        var (task, jellyfin, directories) = Build();
        var media = NewMedia(MediaSourceType.Jellyfin);
        jellyfin.Images.Add(new JellyfinPreviewImage("primary", new byte[] { 1, 2, 3 }));
        jellyfin.Images.Add(new JellyfinPreviewImage("thumb", new byte[] { 4, 5 }));

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        var outputDir = Path.Combine(directories[DirectoryType.Assets], "previews", media.Id.ToString());
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(outputDir, "thumb-001.jpg")));
        Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(Path.Combine(outputDir, "thumb-002.jpg")));
        Assert.Equal("jf-item-1", jellyfin.LastItemId);
        Assert.Equal(3, jellyfin.LastMaxImages);
    }

    [Fact]
    public async Task Execute_ExistingPreview_AndOverwriteFalse_SkipsDownload()
    {
        var (task, jellyfin, directories) = Build();
        var media = NewMedia(MediaSourceType.Jellyfin);
        jellyfin.Images.Add(new JellyfinPreviewImage("primary", new byte[] { 9 }));

        var outputDir = Path.Combine(directories[DirectoryType.Assets], "previews", media.Id.ToString());
        Directory.CreateDirectory(outputDir);
        var existingPath = Path.Combine(outputDir, "thumb-001.jpg");
        await File.WriteAllBytesAsync(existingPath, new byte[] { 1 });

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(existingPath));
        Assert.Equal(0, jellyfin.CallCount);
    }

    [Fact]
    public async Task Execute_LocalMedia_SkipsDownload()
    {
        var (task, jellyfin, _) = Build();

        await task.ExecuteAsync(NewContext(NewMedia(MediaSourceType.LocalFile)), CancellationToken.None);

        Assert.Equal(0, jellyfin.CallCount);
    }

    private (JellyfinPreviewTask Task, StubJellyfin Jellyfin, DirectoriesConfig Directories) Build()
    {
        var jellyfin = new StubJellyfin();
        var directories = new DirectoriesConfig(_rootDir, Enum.GetNames<DirectoryType>());
        var config = new PipelineConfig();
        var task = new JellyfinPreviewTask(jellyfin, directories, config);

        return (task, jellyfin, directories);
    }

    private static ChannelMediaEntity NewMedia(MediaSourceType sourceType)
    {
        return new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            SourceType = sourceType,
            SourceRef = "jf-item-1"
        };
    }

    private static PipelineContext NewContext(ChannelMediaEntity media)
    {
        return new PipelineContext(
            media,
            new MediaLibraryEntity { Id = Guid.NewGuid(), Name = "L", IsActive = true, SourceRef = "/x" },
            "/some/file.mp4",
            new JobExecutionContext(Guid.NewGuid(), "{}", Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}"), new InMemoryJobBus())
        );
    }

    private sealed class StubJellyfin : IJellyfinService
    {
        public List<JellyfinPreviewImage> Images { get; } = new();
        public string? LastItemId { get; private set; }
        public int LastMaxImages { get; private set; }
        public int CallCount { get; private set; }

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
            throw new NotSupportedException();
        }

        public Task<JellyfinItemTags> GetItemTagsAsync(string itemId, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<JellyfinPreviewImage>> GetPreviewImagesAsync(
            string itemId,
            int maxImages,
            CancellationToken ct = default
        )
        {
            CallCount++;
            LastItemId = itemId;
            LastMaxImages = maxImages;
            return Task.FromResult<IReadOnlyList<JellyfinPreviewImage>>(Images);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
