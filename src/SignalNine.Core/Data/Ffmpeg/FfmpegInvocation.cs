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

    public static FfmpegInvocation BuildHls(FfmpegHlsInvocationOptions o)
    {
        ArgumentNullException.ThrowIfNull(o);

        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "warning",
            "-ss", o.SkipSeconds.ToString(CultureInfo.InvariantCulture),
            "-i", o.SourcePath,
            "-t", o.DurationCapSeconds.ToString(CultureInfo.InvariantCulture),
            "-vf", o.VideoFilter,
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-tune", "zerolatency",
            "-b:v", $"{o.VideoBitrateKbps}k",
            "-maxrate", $"{(int)(o.VideoBitrateKbps * 1.2)}k",
            "-bufsize", $"{o.VideoBitrateKbps * 2}k",
            "-g", (o.Fps * 2).ToString(CultureInfo.InvariantCulture),
            "-keyint_min", o.Fps.ToString(CultureInfo.InvariantCulture),
            "-sc_threshold", "0",
            "-c:a", "aac",
            "-b:a", "128k",
            "-ac", "2",
            "-ar", "48000",
            "-f", "hls",
            "-hls_time", o.HlsTimeSeconds.ToString(CultureInfo.InvariantCulture),
            "-hls_list_size", o.HlsListSize.ToString(CultureInfo.InvariantCulture),
            "-hls_flags", "+append_list+delete_segments+independent_segments+omit_endlist",
            "-hls_segment_filename", o.SegmentFilenamePattern,
            "-hls_segment_type", "mpegts",
            "-start_number", o.StartSegmentNumber.ToString(CultureInfo.InvariantCulture),
            "-output_ts_offset", o.OutputTsOffsetSeconds.ToString(CultureInfo.InvariantCulture),
            o.PlaylistPath
        };

        return new FfmpegInvocation(o.FfmpegExecutable, args.ToArray());
    }
}
