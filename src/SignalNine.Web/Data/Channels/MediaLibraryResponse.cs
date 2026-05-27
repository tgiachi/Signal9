using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Channels;

public record MediaLibraryResponse(
    Guid Id,
    string Name,
    string? Description,
    ChannelMediaType DefaultMediaType,
    MediaSourceType SourceType,
    string SourceRef,
    bool IsActive,
    DateTime? LastScannedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
