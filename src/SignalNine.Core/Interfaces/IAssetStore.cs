namespace SignalNine.Core.Interfaces;

public interface IAssetStore
{
    Task PutPreviewAsync(Guid mediaId, string filename, string sourcePath, CancellationToken cancellationToken = default);
    Task DeletePreviewsAsync(Guid mediaId, CancellationToken cancellationToken = default);
    string GetPreviewUrl(Guid mediaId, int index);
}
