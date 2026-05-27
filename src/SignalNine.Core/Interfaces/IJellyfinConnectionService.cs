using SignalNine.Core.Data.Jellyfin;

namespace SignalNine.Core.Interfaces;

public interface IJellyfinConnectionService
{
    Task<JellyfinConnectionStatus> GetStatusAsync(CancellationToken ct = default);
    Task SetAsync(string baseUrl, string apiKey, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<(string BaseUrl, string ApiKey)?> GetCredentialsAsync(CancellationToken ct = default);
    Task MarkVerifiedAsync(CancellationToken ct = default);
}
