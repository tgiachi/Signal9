namespace SignalNine.Web.Data.Channels;

public record CreateChannelRequest(
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    int DisplayOrder,
    bool CommercialsEnabled,
    int CommercialIntervalMinSeconds,
    int CommercialIntervalMaxSeconds
);
