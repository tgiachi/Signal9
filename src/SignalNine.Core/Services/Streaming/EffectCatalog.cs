using SignalNine.Core.Data.Streaming;

namespace SignalNine.Core.Services.Streaming;

public static class EffectCatalog
{
    public static readonly IReadOnlyList<EffectDescriptor> Items = new EffectDescriptor[]
    {
        new(
            "vhs",
            "VHS",
            "Noise + vintage colour curves",
            new[]
            {
                new EffectParameterDescriptor("intensity", "Intensità", "number", 0, 1, 0.05, 0.5)
            },
            p => "noise=alls=" + (int)(p["intensity"] * 30) + ":allf=t,curves=preset=vintage"
        ),
        new(
            "scanlines",
            "Scanlines CRT",
            "Alternating brightness rows simulating a CRT",
            new[]
            {
                new EffectParameterDescriptor("intensity", "Intensità", "number", 0, 1, 0.05, 0.3)
            },
            p =>
                "geq=lum='lum(X\\,Y)*(1-" +
                (p["intensity"] * 0.4).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "*mod(Y\\,2))'"
        ),
        new(
            "blackwhite",
            "B&W",
            "Desaturate to monochrome",
            Array.Empty<EffectParameterDescriptor>(),
            _ => "hue=s=0"
        )
    };
}
