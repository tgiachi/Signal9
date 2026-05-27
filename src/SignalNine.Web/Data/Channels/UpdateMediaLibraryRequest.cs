using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Channels;

public record UpdateMediaLibraryRequest(
    string Name,
    string? Description,
    ChannelMediaType DefaultMediaType,
    bool IsActive,
    MediaSourceType SourceType,
    string SourceRef
);
