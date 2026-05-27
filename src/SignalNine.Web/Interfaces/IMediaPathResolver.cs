using SignalNine.Persistence.Entities.Channels;

namespace SignalNine.Web.Interfaces;

public interface IMediaPathResolver
{
    Task<string> ResolveAsync(
        ChannelMediaEntity media,
        MediaLibraryEntity library,
        CancellationToken ct
    );
}
