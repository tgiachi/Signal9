using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public sealed class FileSystemAssetStore : IAssetStore
{
    private readonly string _rootPath;

    public FileSystemAssetStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = rootPath;
    }

    public Task PutPreviewAsync(Guid mediaId, string filename, string sourcePath, CancellationToken cancellationToken = default)
    {
        var dest = Path.Combine(_rootPath, "previews", mediaId.ToString(), filename);
        var dir = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(dir);
        File.Copy(sourcePath, dest, overwrite: true);
        return Task.CompletedTask;
    }

    public Task DeletePreviewsAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_rootPath, "previews", mediaId.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    public string GetPreviewUrl(Guid mediaId, int index)
        => $"/assets/previews/{mediaId}/thumb-{index:D3}.jpg";
}
