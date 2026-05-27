namespace SignalNine.Core.Data.Ffmpeg;

public record FfprobeResult(
    TimeSpan? Duration,
    long? SizeBytes,
    string? FormatName,
    IReadOnlyList<FfprobeStream> Streams
);
