using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class FileSystemAssetStoreTests : IDisposable
{
    private readonly string _root;

    public FileSystemAssetStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"signal9-assets-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task PutPreview_copies_file_to_destination()
    {
        var src = Path.Combine(Path.GetTempPath(), $"src-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(src, new byte[] { 0xFF, 0xD8 });

        var store = new FileSystemAssetStore(_root);
        var mediaId = Guid.NewGuid();
        await store.PutPreviewAsync(mediaId, "thumb-001.jpg", src);

        var dest = Path.Combine(_root, "previews", mediaId.ToString(), "thumb-001.jpg");
        Assert.True(File.Exists(dest));
        Assert.Equal(2, (await File.ReadAllBytesAsync(dest)).Length);

        File.Delete(src);
    }

    [Fact]
    public async Task DeletePreviews_removes_media_folder()
    {
        var store = new FileSystemAssetStore(_root);
        var mediaId = Guid.NewGuid();
        var dir = Path.Combine(_root, "previews", mediaId.ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "x.jpg"), "x");

        await store.DeletePreviewsAsync(mediaId);

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void GetPreviewUrl_formats_index_padded()
    {
        var store = new FileSystemAssetStore(_root);
        var mediaId = Guid.NewGuid();
        var url = store.GetPreviewUrl(mediaId, 7);
        Assert.Equal($"/assets/previews/{mediaId}/thumb-007.jpg", url);
    }
}
