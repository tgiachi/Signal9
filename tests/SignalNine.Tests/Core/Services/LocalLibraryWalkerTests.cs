using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class LocalLibraryWalkerTests : IDisposable
{
    private readonly string _root;

    public LocalLibraryWalkerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"signal9-walker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }

    private string Touch(string relativePath, long size = 1)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var fs = File.Create(full);
        if (size > 0) fs.Write(new byte[size]);
        return full;
    }

    [Fact]
    public void Walk_YieldsOnlyMediaExtensions_WithRelativePaths()
    {
        Touch("a.mp4");
        Touch("sub/b.mkv");
        Touch("notes.txt");
        Touch("subs/sample.srt");

        var walker = new LocalLibraryWalker();
        var items = walker.Walk(_root, CancellationToken.None).ToList();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.RelativePath == "a.mp4" && i.Title == "a");
        Assert.Contains(items, i => i.RelativePath == "sub/b.mkv" && i.Title == "b");
    }

    [Fact]
    public void Walk_MixedCaseExtensions_AreIncluded()
    {
        Touch("Movie.MP4");
        Touch("Show.Mkv");

        var walker = new LocalLibraryWalker();
        var items = walker.Walk(_root, CancellationToken.None).ToList();

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Walk_MissingRoot_ThrowsDirectoryNotFoundException()
    {
        var walker = new LocalLibraryWalker();
        var missing = Path.Combine(_root, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() => walker.Walk(missing, CancellationToken.None).ToList());
    }

    [Fact]
    public void Walk_EmptyDirectory_ReturnsEmpty()
    {
        var walker = new LocalLibraryWalker();
        var items = walker.Walk(_root, CancellationToken.None).ToList();
        Assert.Empty(items);
    }

    [Fact]
    public void Walk_RelativePath_UsesForwardSlashes()
    {
        Touch(Path.Combine("nested", "deep", "movie.mp4"));

        var walker = new LocalLibraryWalker();
        var items = walker.Walk(_root, CancellationToken.None).ToList();

        Assert.Single(items);
        Assert.Equal("nested/deep/movie.mp4", items[0].RelativePath);
    }

    [Fact]
    public void Walk_Cancellation_StopsEnumeration()
    {
        for (int i = 0; i < 50; i++)
        {
            Touch($"f{i}.mp4");
        }

        var walker = new LocalLibraryWalker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => walker.Walk(_root, cts.Token).ToList());
    }

    [Fact]
    public void Walk_RecordsFileSize()
    {
        Touch("a.mp4", size: 1234);

        var walker = new LocalLibraryWalker();
        var items = walker.Walk(_root, CancellationToken.None).ToList();

        Assert.Single(items);
        Assert.Equal(1234, items[0].FileSizeBytes);
    }
}
