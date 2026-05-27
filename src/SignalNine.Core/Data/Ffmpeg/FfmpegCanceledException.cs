namespace SignalNine.Core.Data.Ffmpeg;

public class FfmpegCanceledException : OperationCanceledException
{
    public FfmpegCanceledException() : base("FFmpeg process was canceled.") { }
}
