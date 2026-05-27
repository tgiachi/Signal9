namespace SignalNine.Core.Data.Ffmpeg;

public class FfmpegNotFoundException : Exception
{
    public FfmpegNotFoundException(string executable)
        : base($"Executable '{executable}' was not found on PATH or at the configured location.") { }
}
