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
}
