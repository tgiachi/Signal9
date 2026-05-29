using SignalNine.Core.Data.Ffmpeg;

namespace SignalNine.Tests.Core.Data.Ffmpeg;

public class FfmpegHlsBuilderTests
{
    private static FfmpegHlsInvocationOptions Sample()
    {
        return new FfmpegHlsInvocationOptions(
            FfmpegExecutable: "ffmpeg",
            SourcePath: "/m/file.mkv",
            SkipSeconds: 12.5,
            DurationCapSeconds: 600,
            VideoFilter: "scale=1280:720,format=yuv420p",
            VideoBitrateKbps: 2500,
            Fps: 25,
            StartSegmentNumber: 42,
            OutputTsOffsetSeconds: 168.0,
            HlsListSize: 6,
            HlsTimeSeconds: 4,
            SegmentFilenamePattern: "/o/seg%05d.ts",
            PlaylistPath: "/o/index.m3u8"
        );
    }

    [Fact]
    public void BuildHls_IncludesSourceAndSeek()
    {
        var inv = FfmpegInvocation.BuildHls(Sample());
        Assert.Contains("-ss", inv.Arguments);
        Assert.Contains("12.5", inv.Arguments);
        Assert.Contains("-i", inv.Arguments);
        Assert.Contains("/m/file.mkv", inv.Arguments);
    }

    [Fact]
    public void BuildHls_IncludesDurationCap()
    {
        var inv = FfmpegInvocation.BuildHls(Sample());
        Assert.Contains("-t", inv.Arguments);
        Assert.Contains("600", inv.Arguments);
    }

    [Fact]
    public void BuildHls_IncludesVideoFilter()
    {
        var inv = FfmpegInvocation.BuildHls(Sample());
        Assert.Contains("-vf", inv.Arguments);
        Assert.Contains("scale=1280:720,format=yuv420p", inv.Arguments);
    }

    [Fact]
    public void BuildHls_IncludesStartNumberAndTsOffset()
    {
        var inv = FfmpegInvocation.BuildHls(Sample());
        Assert.Contains("-start_number", inv.Arguments);
        Assert.Contains("42", inv.Arguments);
        Assert.Contains("-output_ts_offset", inv.Arguments);
        Assert.Contains("168", inv.Arguments);
    }

    [Fact]
    public void BuildHls_IncludesHlsKnobs()
    {
        var inv = FfmpegInvocation.BuildHls(Sample());
        Assert.Contains("-f", inv.Arguments);
        Assert.Contains("hls", inv.Arguments);
        Assert.Contains("-hls_time", inv.Arguments);
        Assert.Contains("4", inv.Arguments);
        Assert.Contains("-hls_list_size", inv.Arguments);
        Assert.Contains("6", inv.Arguments);
        Assert.Contains("+append_list+delete_segments+independent_segments+omit_endlist", inv.Arguments);
        Assert.Contains("/o/seg%05d.ts", inv.Arguments);
        Assert.Contains("/o/index.m3u8", inv.Arguments);
    }
}
