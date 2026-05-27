using System.Globalization;
using System.Text.Json;
using SignalNine.Core.Data.Ffmpeg;

namespace SignalNine.Core.Services.Ffmpeg;

public static class FfprobeJsonParser
{
    public static FfprobeResult Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var streams = ParseStreams(root);
        var (duration, sizeBytes, formatName) = ParseFormat(root);

        return new FfprobeResult(duration, sizeBytes, formatName, streams);
    }

    private static IReadOnlyList<FfprobeStream> ParseStreams(JsonElement root)
    {
        if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FfprobeStream>();
        }

        var list = new List<FfprobeStream>(streams.GetArrayLength());
        foreach (var s in streams.EnumerateArray())
        {
            list.Add(
                new FfprobeStream(
                    Index: GetInt(s, "index") ?? 0,
                    CodecType: GetString(s, "codec_type") ?? string.Empty,
                    CodecName: GetString(s, "codec_name"),
                    Width: GetInt(s, "width"),
                    Height: GetInt(s, "height"),
                    Channels: GetInt(s, "channels"),
                    Language: s.TryGetProperty("tags", out var tags)
                              && tags.ValueKind == JsonValueKind.Object
                              && tags.TryGetProperty("language", out var lang)
                                  ? lang.GetString()
                                  : null
                )
            );
        }
        return list;
    }

    private static (TimeSpan? Duration, long? SizeBytes, string? FormatName) ParseFormat(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var fmt) || fmt.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        TimeSpan? duration = null;
        if (fmt.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.String)
        {
            if (double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                duration = TimeSpan.FromSeconds(seconds);
            }
        }

        long? size = null;
        if (fmt.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.String)
        {
            if (long.TryParse(sz.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            {
                size = bytes;
            }
        }

        var formatName = GetString(fmt, "format_name");

        return (duration, size, formatName);
    }

    private static string? GetString(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? GetInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out var i) ? i : null,
            _ => null
        };
    }
}
