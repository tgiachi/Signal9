using SignalNine.Core.Data.Streaming;

namespace SignalNine.Web.Data.Streaming;

public sealed record ChannelEffectsRequest(IReadOnlyList<ChannelEffect> Effects);
