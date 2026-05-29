namespace SignalNine.Core.Data.Ffmpeg;

public sealed record FfmpegHlsInvocationOptions(
    string FfmpegExecutable,
    string SourcePath,
    double SkipSeconds,
    double DurationCapSeconds,
    string VideoFilter,
    int VideoBitrateKbps,
    int Fps,
    int StartSegmentNumber,
    double OutputTsOffsetSeconds,
    int HlsListSize,
    int HlsTimeSeconds,
    string SegmentFilenamePattern,
    string PlaylistPath
);
