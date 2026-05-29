using SignalNine.Core.Data.Streaming;
using SignalNine.Core.Services.Streaming;

namespace SignalNine.Tests.Core.Services.Streaming;

public class EffectCatalogTests
{
    [Fact]
    public void Items_ContainsVhsScanlinesAndBlackwhite()
    {
        var kinds = EffectCatalog.Items.Select(d => d.Kind).ToList();
        Assert.Contains("vhs", kinds);
        Assert.Contains("scanlines", kinds);
        Assert.Contains("blackwhite", kinds);
    }

    [Fact]
    public void Vhs_HasIntensityParameter()
    {
        var vhs = EffectCatalog.Items.Single(d => d.Kind == "vhs");
        var param = Assert.Single(vhs.Parameters);
        Assert.Equal("intensity", param.Name);
    }

    [Fact]
    public void Items_ContainsAllTenPresets()
    {
        var expected = new[]
        {
            "vhs", "scanlines", "blackwhite", "sepia", "vignette",
            "grain8mm", "brightness", "contrast", "saturation", "colortemp"
        };
        var actual = EffectCatalog.Items.Select(d => d.Kind).ToList();
        foreach (var kind in expected)
        {
            Assert.Contains(kind, actual);
        }
    }

    [Fact]
    public void BuildFilter_NoEffects_ReturnsScaleAndFormatOnly()
    {
        var vf = EffectCatalog.BuildFilter(Array.Empty<ChannelEffect>(), 1280, 720);
        Assert.Equal("scale=1280:720:flags=lanczos,format=yuv420p", vf);
    }

    [Fact]
    public void BuildFilter_DisabledEffectsSkipped()
    {
        var effects = new[]
        {
            new ChannelEffect("blackwhite", false, new Dictionary<string, double>())
        };
        var vf = EffectCatalog.BuildFilter(effects, 1280, 720);
        Assert.Equal("scale=1280:720:flags=lanczos,format=yuv420p", vf);
    }

    [Fact]
    public void BuildFilter_UnknownKindSilentlySkipped()
    {
        var effects = new[]
        {
            new ChannelEffect("nonexistent", true, new Dictionary<string, double>())
        };
        var vf = EffectCatalog.BuildFilter(effects, 640, 360);
        Assert.Equal("scale=640:360:flags=lanczos,format=yuv420p", vf);
    }

    [Fact]
    public void BuildFilter_MissingParameterUsesDefault()
    {
        var effects = new[]
        {
            new ChannelEffect("vhs", true, new Dictionary<string, double>())
        };
        var vf = EffectCatalog.BuildFilter(effects, 1280, 720);
        Assert.Contains("noise=alls=15:allf=t", vf);
    }

    [Fact]
    public void BuildFilter_ChainPreservesOrder()
    {
        var effects = new[]
        {
            new ChannelEffect("blackwhite", true, new Dictionary<string, double>()),
            new ChannelEffect("vignette", true, new Dictionary<string, double> { ["strength"] = 0.5 })
        };
        var vf = EffectCatalog.BuildFilter(effects, 1280, 720);
        var bwIdx = vf.IndexOf("hue=s=0", StringComparison.Ordinal);
        var vigIdx = vf.IndexOf("vignette=", StringComparison.Ordinal);
        Assert.True(bwIdx > 0 && vigIdx > bwIdx);
    }
}
