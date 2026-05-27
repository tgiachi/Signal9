namespace SignalNine.Core.Data.Ffmpeg;

public enum FfmpegProcessStatusType
{
    Queued = 0,
    Starting = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Canceled = 5
}
