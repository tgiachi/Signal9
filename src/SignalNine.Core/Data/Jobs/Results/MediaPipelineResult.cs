using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Data.Jobs.Results;

public sealed record MediaPipelineResult(
    Guid ChannelMediaId,
    int? DurationSeconds,
    IReadOnlyList<string> PreviewFiles,
    string? ProbeJson
) : IJobResult
{
    public string Type => "media.pipeline";
}
