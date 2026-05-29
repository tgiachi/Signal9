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
    DateTime UpdatedAt,
    int CommercialBreakSize,
    int CommercialIntervalJitterSeconds,
    bool CommercialBumpersEnabled,
    string? FallbackTagFilterCsv,
    string? FallbackTypeFilterCsv,
    int OutputWidth,
    int OutputHeight,
    int OutputVideoBitrateKbps,
    string? VideoEffectsJson
);
