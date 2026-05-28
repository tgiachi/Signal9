using SignalNine.Core.Data.Config;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web.Services;

public class WorkSpaceStagerTests : IDisposable
{
    private readonly string _tempRoot;

    public WorkSpaceStagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"signalnine-stager-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }

    private WorkSpaceStager BuildStager()
    {
        var config = new SignalNineConfig
        {
            WorkSpace = new WorkSpaceConfig { Path = _tempRoot }
        };
        return new WorkSpaceStager(config);
    }

    [Fact]
    public async Task StageAsync_LocalFile_HappyPath_CopiesFileAndReturnsCorrectPaths()
    {
        var libraryRoot = Path.Combine(_tempRoot, "library");
        Directory.CreateDirectory(libraryRoot);
        var sourceFile = Path.Combine(libraryRoot, "movie.mp4");
        var sourceBytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(sourceFile, sourceBytes);

        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = libraryRoot
        };
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "movie.mp4"
        };

        var stager = BuildStager();
        var (workDir, relativeInputFile) = await stager.StageAsync(media, library);

        Assert.StartsWith(Path.Combine(_tempRoot, "jobs"), workDir, StringComparison.Ordinal);
        Assert.Equal("input/movie.mp4", relativeInputFile);

        var destPath = Path.Combine(workDir, relativeInputFile);
        Assert.True(File.Exists(destPath));
        var destBytes = await File.ReadAllBytesAsync(destPath);
        Assert.Equal(sourceBytes, destBytes);
    }

    [Fact]
    public async Task StageAsync_LocalFile_NestedSubdirectory_FlattensToBasename()
    {
        var libraryRoot = Path.Combine(_tempRoot, "library");
        var subDir = Path.Combine(libraryRoot, "subdir");
        Directory.CreateDirectory(subDir);
        var sourceFile = Path.Combine(subDir, "episode.mkv");
        await File.WriteAllBytesAsync(sourceFile, new byte[] { 10, 20, 30 });

        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = libraryRoot
        };
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "subdir/episode.mkv"
        };

        var stager = BuildStager();
        var (workDir, relativeInputFile) = await stager.StageAsync(media, library);

        Assert.Equal("input/episode.mkv", relativeInputFile);
        Assert.True(File.Exists(Path.Combine(workDir, relativeInputFile)));
    }

    [Fact]
    public async Task StageAsync_LocalFile_MissingOnDisk_ThrowsFileNotFoundException()
    {
        var libraryRoot = Path.Combine(_tempRoot, "library");
        Directory.CreateDirectory(libraryRoot);

        var mediaId = Guid.NewGuid();
        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = libraryRoot
        };
        var media = new ChannelMediaEntity
        {
            Id = mediaId,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "missing.mp4"
        };

        var stager = BuildStager();
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => stager.StageAsync(media, library)
        );

        Assert.Contains(mediaId.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Contains("missing.mp4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageAsync_JellyfinSource_ReturnsNonEmptyWorkDirAndEmptyRelativeInput()
    {
        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-library-id"
        };
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-id"
        };

        var stager = BuildStager();
        var (workDir, relativeInputFile) = await stager.StageAsync(media, library);

        Assert.False(string.IsNullOrEmpty(workDir));
        Assert.Equal(string.Empty, relativeInputFile);
        Assert.True(Directory.Exists(workDir));
    }

    [Fact]
    public async Task StageAsync_CalledTwice_ReturnsDifferentWorkDirs()
    {
        var libraryRoot = Path.Combine(_tempRoot, "library");
        Directory.CreateDirectory(libraryRoot);
        var sourceFile = Path.Combine(libraryRoot, "movie.mp4");
        await File.WriteAllBytesAsync(sourceFile, new byte[] { 1, 2, 3 });

        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = libraryRoot
        };
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "movie.mp4"
        };

        var stager = BuildStager();
        var (workDir1, _) = await stager.StageAsync(media, library);
        var (workDir2, _) = await stager.StageAsync(media, library);

        Assert.NotEqual(workDir1, workDir2);
    }
}
