namespace SignalNine.Core.Data.Ffmpeg;

public record FfmpegProgressUpdate(
    long FrameCount,
    double Fps,
    TimeSpan OutTime,
    double Speed,
    long BytesProcessed
);
