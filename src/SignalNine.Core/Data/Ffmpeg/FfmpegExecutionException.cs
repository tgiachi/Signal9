namespace SignalNine.Core.Data.Ffmpeg;

public class FfmpegExecutionException : Exception
{
    public int? ExitCode { get; }
    public string LastOutput { get; }

    public FfmpegExecutionException(string message, int? exitCode, string lastOutput)
        : base(message)
    {
        ExitCode = exitCode;
        LastOutput = lastOutput;
    }
}
