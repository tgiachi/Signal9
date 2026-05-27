using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;
using SignalNine.Core.Services.Ffmpeg;

namespace SignalNine.Tests.Core.Services.Ffmpeg;

[Trait("Category", "FfmpegIntegration")]
public class FfprobeIntegrationTests
{
    [Fact]
    public async Task Probe_RealFfprobe_ReturnsDuration()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ffmpeg", "sample.mp4");
        Assert.True(File.Exists(fixturePath), $"Fixture not found at {fixturePath}");

        var pool = new FfmpegPool(new DefaultProcessLauncher(), new FfmpegPoolConfig());

        var result = await pool.ProbeAsync(fixturePath);

        Assert.NotNull(result.Duration);
        Assert.InRange(result.Duration!.Value.TotalSeconds, 1.5, 2.5);
        Assert.Contains(result.Streams, s => s.CodecType == "video");
    }
}
