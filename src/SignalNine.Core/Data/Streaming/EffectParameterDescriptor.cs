namespace SignalNine.Core.Data.Streaming;

public sealed record EffectParameterDescriptor(
    string Name,
    string Label,
    string Type,
    double? Min,
    double? Max,
    double? Step,
    double Default
);
