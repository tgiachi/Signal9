using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Interfaces;

namespace SignalNine.Jobs.Services.Pipeline;

public class ProbeMediaTask : IPipelineTask
{
    private readonly IFfmpegPool _pool;
    private readonly IDataAccess<ChannelMediaEntity> _media;
    private readonly PipelineConfig _config;

    public string Name => "probe";
    public int Order => 100;
    public bool IsEnabled => _config.Tasks.Probe.Enabled;

    public ProbeMediaTask(
        IFfmpegPool pool,
        IDataAccess<ChannelMediaEntity> media,
        PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(config);

        _pool = pool;
        _media = media;
        _config = config;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Media.DurationSeconds is not null && !_config.Tasks.Probe.OverwriteExisting)
        {
            return;
        }

        if (context.Media.SourceType == MediaSourceType.Jellyfin &&
            !_config.Tasks.Probe.AllowJellyfinStreamProbe)
        {
            return;
        }

        var probe = await _pool.ProbeAsync(context.ResolvedPath, ct).ConfigureAwait(false);
        if (probe.Duration is null)
        {
            return;
        }

        context.Media.DurationSeconds = (int)probe.Duration.Value.TotalSeconds;
        context.Media.UpdatedAt = DateTime.UtcNow;
        _media.Update(context.Media);
    }
}
