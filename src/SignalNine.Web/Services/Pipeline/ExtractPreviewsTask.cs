using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Types;
using SignalNine.Web.Data.Pipeline;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class ExtractPreviewsTask : IPipelineTask
{
    private const string PreviewsRootName = "previews";
    private const string ThumbnailPattern = "thumb-%03d.jpg";
    private const string ExistingThumbnailPattern = "thumb-*.jpg";
    private const string FfmpegExecutable = "ffmpeg";

    private readonly IFfmpegPool _pool;
    private readonly DirectoriesConfig _directories;
    private readonly PipelineConfig _config;

    public string Name => "preview";
    public int Order => 200;
    public bool IsEnabled => _config.Tasks.Preview.Enabled;

    public ExtractPreviewsTask(
        IFfmpegPool pool,
        DirectoriesConfig directories,
        PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(config);

        _pool = pool;
        _directories = directories;
        _config = config;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outputDir = Path.Combine(
            _directories[DirectoryType.Assets],
            PreviewsRootName,
            context.Media.Id.ToString()
        );

        if (!_config.Tasks.Preview.OverwriteExisting && HasExistingPreview(outputDir))
        {
            return;
        }

        if (context.Media.SourceType == MediaSourceType.Jellyfin
            && !_config.Tasks.Preview.AllowJellyfinStreamFallback)
        {
            return;
        }

        var durationSeconds = context.Media.DurationSeconds;
        if (durationSeconds is null)
        {
            var probe = await _pool.ProbeAsync(context.ResolvedPath, ct).ConfigureAwait(false);
            if (probe.Duration is null || probe.Duration.Value.TotalSeconds <= 0)
            {
                return;
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
            context.ResolvedPath,
            outputPattern,
            _config.Tasks.Preview.PreviewCount,
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
    }

    private static bool HasExistingPreview(string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return false;
        }

        return Directory.EnumerateFiles(outputDir, ExistingThumbnailPattern).Any();
    }
}
