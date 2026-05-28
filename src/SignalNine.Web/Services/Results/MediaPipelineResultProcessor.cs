using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;

namespace SignalNine.Web.Services.Results;

public sealed class MediaPipelineResultProcessor : IJobResultProcessor
{
    public const string TargetType = "media.pipeline";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAssetStore _assetStore;
    private readonly WorkSpaceConfig _workspace;

    public MediaPipelineResultProcessor(
        IServiceScopeFactory scopeFactory,
        IAssetStore assetStore,
        SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(assetStore);
        ArgumentNullException.ThrowIfNull(config);

        _scopeFactory = scopeFactory;
        _assetStore = assetStore;
        _workspace = config.WorkSpace;
    }

    public string JobType => TargetType;

    public async Task ApplyAsync(Guid jobId, string? resultJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return;

        var result = JsonSerializer.Deserialize<MediaPipelineResult>(resultJson)
                     ?? throw new InvalidOperationException("Empty pipeline result.");

        // 1) DB update — only if duration was actually probed
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var media = scope.ServiceProvider.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
            var entity = media.GetByKey(result.ChannelMediaId);
            if (entity is not null && result.DurationSeconds is { } durationSeconds)
            {
                entity.DurationSeconds = durationSeconds;
                entity.UpdatedAt = DateTime.UtcNow;
                media.Update(entity);
            }
        }

        // 2) Copy thumbnails from workdir into the asset store
        var workDir = Path.Combine(_workspace.Path, "jobs", jobId.ToString());
        var outputDir = Path.Combine(workDir, "output");
        foreach (var filename in result.PreviewFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var src = Path.Combine(outputDir, filename);
            if (File.Exists(src))
            {
                await _assetStore.PutPreviewAsync(result.ChannelMediaId, filename, src, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // 3) Cleanup
        if (_workspace.CleanupAfterProcessing && Directory.Exists(workDir))
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch { /* tolerate — janitor will reap orphans later */ }
        }
    }
}
