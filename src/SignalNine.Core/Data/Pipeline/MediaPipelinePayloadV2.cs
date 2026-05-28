namespace SignalNine.Core.Data.Pipeline;

public sealed record MediaPipelinePayloadV2(
    Guid ChannelMediaId,
    string InputFile,    // relative path inside WorkDir, e.g. "input/movie.mp4"
    int PreviewCount = 5
);
