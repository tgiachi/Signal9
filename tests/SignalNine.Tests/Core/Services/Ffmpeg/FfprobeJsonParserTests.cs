using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Services.Ffmpeg;

namespace SignalNine.Tests.Core.Services.Ffmpeg;

public class FfprobeJsonParserTests
{
    private const string MovieJson = """
    {
      "streams": [
        { "index": 0, "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080 },
        { "index": 1, "codec_type": "audio", "codec_name": "aac", "channels": 2, "tags": { "language": "eng" } }
      ],
      "format": {
        "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
        "duration": "7321.234567",
        "size": "1048576000"
      }
    }
    """;

    [Fact]
    public void Parse_ExtractsDurationAndStreams()
    {
        var result = FfprobeJsonParser.Parse(MovieJson);

        Assert.NotNull(result.Duration);
        Assert.Equal(7321.234567, result.Duration!.Value.TotalSeconds, precision: 3);
        Assert.Equal(1048576000L, result.SizeBytes);
        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", result.FormatName);
        Assert.Equal(2, result.Streams.Count);

        var video = result.Streams[0];
        Assert.Equal("video", video.CodecType);
        Assert.Equal("h264", video.CodecName);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Null(video.Channels);
        Assert.Null(video.Language);

        var audio = result.Streams[1];
        Assert.Equal("audio", audio.CodecType);
        Assert.Equal(2, audio.Channels);
        Assert.Equal("eng", audio.Language);
        Assert.Null(audio.Width);
    }

    [Fact]
    public void Parse_EmptyFormat_ReturnsNullDuration()
    {
        var json = """{ "streams": [], "format": {} }""";
        var result = FfprobeJsonParser.Parse(json);

        Assert.Null(result.Duration);
        Assert.Null(result.SizeBytes);
        Assert.Null(result.FormatName);
        Assert.Empty(result.Streams);
    }

    [Fact]
    public void Parse_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => FfprobeJsonParser.Parse("{ not json"));
    }
}
