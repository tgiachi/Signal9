namespace SignalNine.Core.Data.Streaming;

public sealed record EffectDescriptor(
    string Kind,
    string Label,
    string Description,
    IReadOnlyList<EffectParameterDescriptor> Parameters,
    Func<IReadOnlyDictionary<string, double>, string> RenderFilter
);
