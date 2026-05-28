using System.Text.Json;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;

namespace SignalNine.Jobs.Services.Pipeline;

public class ProbeMediaTask
{
    private readonly IFfmpegPool _pool;
    private readonly PipelineConfig _config;

    public ProbeMediaTask(IFfmpegPool pool, PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(config);

        _pool = pool;
        _config = config;
    }

    public async Task<ProbeResult> RunAsync(string inputPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var probe = await _pool.ProbeAsync(inputPath, ct).ConfigureAwait(false);
        if (probe.Duration is null)
        {
            return new ProbeResult(null, null);
        }

        var json = JsonSerializer.Serialize(probe);
        return new ProbeResult((int)probe.Duration.Value.TotalSeconds, json);
    }
}
