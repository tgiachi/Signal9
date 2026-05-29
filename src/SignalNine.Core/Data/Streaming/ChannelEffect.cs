namespace SignalNine.Core.Data.Streaming;

public sealed record ChannelEffect(
    string Kind,
    bool Enabled,
    Dictionary<string, double> Params
);
