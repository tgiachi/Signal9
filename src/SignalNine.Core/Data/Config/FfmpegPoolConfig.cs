namespace SignalNine.Core.Data.Config;

public class FfmpegPoolConfig
{
    private const int DefaultMaxConcurrent = 2;
    private const int DefaultKillGraceSeconds = 5;
    private const int DefaultOutputBufferLines = 200;
    private const int DefaultRegistryRetention = 100;

    public int MaxConcurrent { get; set; } = DefaultMaxConcurrent;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public int KillGraceSeconds { get; set; } = DefaultKillGraceSeconds;
    public int OutputBufferLines { get; set; } = DefaultOutputBufferLines;
    public int RegistryRetention { get; set; } = DefaultRegistryRetention;
}
