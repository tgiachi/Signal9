using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Jobs.Services.Pipeline;

public class ExtractPreviewsTask
{
    private const string ThumbnailPattern = "thumb-%03d.jpg";
    private const string ExistingThumbnailPattern = "thumb-*.jpg";
    private const string FfmpegExecutable = "ffmpeg";

    private readonly IFfmpegPool _pool;

    public ExtractPreviewsTask(IFfmpegPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
    }

    public async Task<IReadOnlyList<string>> RunAsync(
        string inputPath,
        string outputDir,
        int count,
        int? durationSecondsHint,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        var durationSeconds = durationSecondsHint;
        if (durationSeconds is null || durationSeconds.Value <= 0)
        {
            var probe = await _pool.ProbeAsync(inputPath, ct).ConfigureAwait(false);
            if (probe.Duration is null || probe.Duration.Value.TotalSeconds <= 0)
            {
                return Array.Empty<string>();
            }
            durationSeconds = (int)probe.Duration.Value.TotalSeconds;
        }

        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
        Directory.CreateDirectory(outputDir);

        var outputPattern = Path.Combine(outputDir, ThumbnailPattern);
        var invocation = FfmpegInvocation.ExtractThumbnails(
            FfmpegExecutable,
            inputPath,
            outputPattern,
            count,
            TimeSpan.FromSeconds(durationSeconds.Value)
        );

        var handle = await _pool.RunAsync(invocation, progress: null, ct).ConfigureAwait(false);
        var snapshot = await handle.Completion.ConfigureAwait(false);

        if (snapshot.Status != FfmpegProcessStatusType.Completed)
        {
            throw new FfmpegExecutionException(
                $"ffmpeg preview extraction failed with status {snapshot.Status}.",
                snapshot.ExitCode,
                string.Join('\n', snapshot.RecentOutputLines)
            );
        }

        return Directory
            .EnumerateFiles(outputDir, ExistingThumbnailPattern)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}
