using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class DefaultMediaPathResolver : IMediaPathResolver
{
    private readonly IJellyfinConnectionService _jellyfinConnection;

    public DefaultMediaPathResolver(IJellyfinConnectionService jellyfinConnection)
    {
        ArgumentNullException.ThrowIfNull(jellyfinConnection);
        _jellyfinConnection = jellyfinConnection;
    }

    public async Task<string> ResolveAsync(
        ChannelMediaEntity media,
        MediaLibraryEntity library,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(library);

        switch (media.SourceType)
        {
            case MediaSourceType.LocalFile:
                if (string.IsNullOrWhiteSpace(library.SourceRef))
                {
                    throw new MediaPathResolutionException(
                        $"MediaLibrary {library.Id} has empty SourceRef for LocalFile source."
                    );
                }
                return Path.Combine(library.SourceRef, media.SourceRef ?? string.Empty);

            case MediaSourceType.Jellyfin:
                var creds = await _jellyfinConnection.GetCredentialsAsync(ct).ConfigureAwait(false);
                if (creds is null)
                {
                    throw new MediaPathResolutionException("Jellyfin connection is not configured.");
                }
                var baseUrl = creds.Value.BaseUrl.TrimEnd('/');
                var itemId = Uri.EscapeDataString(media.SourceRef ?? string.Empty);
                var apiKey = Uri.EscapeDataString(creds.Value.ApiKey);
                return $"{baseUrl}/Videos/{itemId}/stream?static=true&api_key={apiKey}";

            case MediaSourceType.Url:
                throw new MediaPathResolutionException("Url source is not supported in v1.");

            default:
                throw new MediaPathResolutionException($"Unknown source type {media.SourceType}.");
        }
    }
}
