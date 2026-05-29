using SignalNine.Core.Data.Streaming;

namespace SignalNine.Web.Data.Streaming;

public sealed record EffectCatalogItemResponse(
    string Kind,
    string Label,
    string Description,
    IReadOnlyList<EffectParameterDescriptor> Parameters
);
