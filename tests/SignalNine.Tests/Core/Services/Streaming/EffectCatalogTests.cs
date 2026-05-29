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
        Assert.Equal(1, vhs.Parameters.Count);
        Assert.Equal("intensity", vhs.Parameters[0].Name);
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
}
