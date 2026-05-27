using SignalNine.Core.Data.Jellyfin;

namespace SignalNine.Core.Interfaces;

public interface IJellyfinService
{
    Task<JellyfinServerInfo> GetServerInfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JellyfinLibrarySummary>> ListLibrariesAsync(CancellationToken ct = default);
    Task<JellyfinItem?> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<IReadOnlyList<JellyfinItem>> ListItemsAsync(string libraryId, CancellationToken ct = default);
}
