namespace SignalNine.Core.Data.Ffmpeg;

public record FfmpegProcessSnapshot(
    Guid Id,
    int? Pid,
    string Executable,
    IReadOnlyList<string> Arguments,
    FfmpegProcessStatusType Status,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    int? ExitCode,
    FfmpegProgressUpdate? LastProgress,
    IReadOnlyList<string> RecentOutputLines,
    string? Error
);
