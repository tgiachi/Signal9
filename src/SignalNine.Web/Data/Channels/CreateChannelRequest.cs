namespace SignalNine.Web.Data.Channels;

public record CreateChannelRequest(
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    int DisplayOrder,
    bool CommercialsEnabled,
    int CommercialIntervalMinSeconds,
    int CommercialIntervalMaxSeconds,
    int CommercialBreakSize = 2,
    int CommercialIntervalJitterSeconds = 120,
    bool CommercialBumpersEnabled = true,
    string? FallbackTagFilterCsv = null,
    string? FallbackTypeFilterCsv = null,
    int OutputWidth = 1280,
    int OutputHeight = 720,
    int OutputVideoBitrateKbps = 2500,
    string? VideoEffectsJson = null
);
