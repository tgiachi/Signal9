namespace SignalNine.Web.Data.Channels;

public record ChannelResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    int DisplayOrder,
    bool IsActive,
    bool CommercialsEnabled,
    int CommercialIntervalMinSeconds,
    int CommercialIntervalMaxSeconds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
