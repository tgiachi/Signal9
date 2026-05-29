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
        ),
        new(
            "sepia",
            "Sepia",
            "Warm vintage tint",
            new[] { new EffectParameterDescriptor("warmth", "Warmth", "number", 0, 1, 0.05, 0.6) },
            _ => "colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131"
        ),
        new(
            "vignette",
            "Vignette",
            "Darkened corners",
            new[] { new EffectParameterDescriptor("strength", "Strength", "number", 0, 1, 0.05, 0.5) },
            p =>
                "vignette=PI/" +
                (4 - p["strength"] * 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
        ),
        new(
            "grain8mm",
            "Grain 8mm",
            "Film grain",
            new[] { new EffectParameterDescriptor("amount", "Amount", "number", 0, 1, 0.05, 0.5) },
            p => "noise=alls=" + (int)(p["amount"] * 40) + ":allf=t+u"
        ),
        new(
            "brightness",
            "Brightness",
            "",
            new[] { new EffectParameterDescriptor("value", "Value", "number", -1, 1, 0.05, 0) },
            p => "eq=brightness=" + p["value"].ToString(System.Globalization.CultureInfo.InvariantCulture)
        ),
        new(
            "contrast",
            "Contrast",
            "",
            new[] { new EffectParameterDescriptor("value", "Value", "number", 0, 3, 0.05, 1) },
            p => "eq=contrast=" + p["value"].ToString(System.Globalization.CultureInfo.InvariantCulture)
        ),
        new(
            "saturation",
            "Saturation",
            "",
            new[] { new EffectParameterDescriptor("value", "Value", "number", 0, 3, 0.05, 1) },
            p => "eq=saturation=" + p["value"].ToString(System.Globalization.CultureInfo.InvariantCulture)
        ),
        new(
            "colortemp",
            "Color Temperature",
            "",
            new[] { new EffectParameterDescriptor("kelvin", "Kelvin", "number", 2000, 10000, 100, 6500) },
            p => "colortemperature=temperature=" + p["kelvin"].ToString(System.Globalization.CultureInfo.InvariantCulture)
        )
    };

    private static readonly Dictionary<string, EffectDescriptor> ByKind = BuildIndex();

    private static Dictionary<string, EffectDescriptor> BuildIndex()
    {
        var dict = new Dictionary<string, EffectDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items)
        {
            dict[item.Kind] = item;
        }
        return dict;
    }

    public static string BuildFilter(IEnumerable<ChannelEffect> effects, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var parts = new List<string> { $"scale={width}:{height}:flags=lanczos" };
        foreach (var effect in effects)
        {
            if (!effect.Enabled) continue;
            if (!ByKind.TryGetValue(effect.Kind, out var descriptor)) continue;

            var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var param in descriptor.Parameters)
            {
                merged[param.Name] = effect.Params.TryGetValue(param.Name, out var v) ? v : param.Default;
            }
            parts.Add(descriptor.RenderFilter(merged));
        }
        parts.Add("format=yuv420p");
        return string.Join(",", parts);
    }
}
