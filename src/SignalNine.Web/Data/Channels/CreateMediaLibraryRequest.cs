using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Channels;

public record CreateMediaLibraryRequest(
    string Name,
    string? Description,
    ChannelMediaType DefaultMediaType,
    MediaSourceType SourceType,
    string SourceRef
);
