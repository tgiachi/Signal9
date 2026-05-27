using System.Globalization;

namespace SignalNine.Core.Data.Ffmpeg;

public record FfmpegInvocation(string Executable, IReadOnlyList<string> Arguments)
{
    public static FfmpegInvocation Probe(string ffprobePath, string inputPath)
    {
        return new FfmpegInvocation(
            ffprobePath,
            new[]
            {
                "-v", "quiet",
                "-print_format", "json",
                "-show_format",
                "-show_streams",
                inputPath
            }
        );
    }

    public static FfmpegInvocation ExtractThumbnails(
        string ffmpegPath,
        string inputPath,
        string outputPattern,
        int count,
        TimeSpan duration
    )
    {
        var interval = duration.TotalSeconds / (count + 1);
        return new FfmpegInvocation(
            ffmpegPath,
            new[]
            {
                "-hide_banner", "-y",
                "-i", inputPath,
                "-vf", $"fps=1/{interval.ToString("0.000", CultureInfo.InvariantCulture)}",
                "-frames:v", count.ToString(CultureInfo.InvariantCulture),
                "-progress", "pipe:1",
                outputPattern
            }
        );
    }

    public static FfmpegInvocation Custom(string executable, IEnumerable<string> args)
    {
        return new FfmpegInvocation(executable, args.ToList());
    }
}
