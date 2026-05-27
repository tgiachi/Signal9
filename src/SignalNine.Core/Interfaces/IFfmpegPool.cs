using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Services.Ffmpeg;

namespace SignalNine.Core.Interfaces;

public interface IFfmpegPool
{
    Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default);

    Task<FfmpegProcessHandle> RunAsync(
        FfmpegInvocation invocation,
        IProgress<FfmpegProgressUpdate>? progress = null,
        CancellationToken ct = default
    );

    IReadOnlyList<FfmpegProcessSnapshot> List();
    FfmpegProcessSnapshot? Get(Guid id);
    Task<bool> CancelAsync(Guid processId, CancellationToken ct = default);
    event EventHandler<FfmpegProcessSnapshot> ProcessChanged;
}
