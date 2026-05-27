namespace SignalNine.Web.Data.Channels;

public record UpdateChannelRequest(
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    int DisplayOrder,
    bool IsActive,
    bool CommercialsEnabled,
    int CommercialIntervalMinSeconds,
    int CommercialIntervalMaxSeconds
);
