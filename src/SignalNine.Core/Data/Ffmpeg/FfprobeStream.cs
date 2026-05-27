namespace SignalNine.Core.Data.Ffmpeg;

public record FfprobeStream(
    int Index,
    string CodecType,
    string? CodecName,
    int? Width,
    int? Height,
    int? Channels,
    string? Language
);
