using SignalNine.Core.Data.Ffmpeg;

namespace SignalNine.Tests.Core.Data.Ffmpeg;

public class FfmpegInvocationTests
{
    [Fact]
    public void Probe_ProducesExpectedArgs()
    {
        var invocation = FfmpegInvocation.Probe("/usr/bin/ffprobe", "/media/movie.mp4");

        Assert.Equal("/usr/bin/ffprobe", invocation.Executable);
        Assert.Equal(
            new[] { "-v", "quiet", "-print_format", "json", "-show_format", "-show_streams", "/media/movie.mp4" },
            invocation.Arguments
        );
    }

    [Fact]
    public void ExtractThumbnails_DistributesAcrossDuration()
    {
        var invocation = FfmpegInvocation.ExtractThumbnails(
            "ffmpeg",
            "/in.mp4",
            "/out/thumb-%03d.jpg",
            count: 4,
            duration: TimeSpan.FromMinutes(2)
        );

        Assert.Equal("ffmpeg", invocation.Executable);
        Assert.Contains("-i", invocation.Arguments);
        Assert.Contains("/in.mp4", invocation.Arguments);
        Assert.Contains("/out/thumb-%03d.jpg", invocation.Arguments);

        var vfIndex = invocation.Arguments.ToList().IndexOf("-vf");
        Assert.True(vfIndex >= 0);
        Assert.Equal("fps=1/24.000", invocation.Arguments[vfIndex + 1]);

        Assert.DoesNotContain(invocation.Arguments, a => a.Contains(','));
    }

    [Fact]
    public void Custom_PassesArgsThroughVerbatim()
    {
        var args = new[] { "-foo", "bar", "/path with space.mp4" };
        var invocation = FfmpegInvocation.Custom("ffmpeg", args);

        Assert.Equal("ffmpeg", invocation.Executable);
        Assert.Equal(args, invocation.Arguments);
    }
}
