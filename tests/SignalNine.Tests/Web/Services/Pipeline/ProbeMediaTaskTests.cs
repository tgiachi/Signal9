using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class ProbeMediaTaskTests
{
    private static (ProbeMediaTask Task, StubPool Pool) Build()
    {
        var pool = new StubPool();
        var config = new PipelineConfig();
        var task = new ProbeMediaTask(pool, config);
        return (task, pool);
    }

    [Fact]
    public async Task RunAsync_ReturnsDuration_WhenProbeSucceeds()
    {
        var (task, pool) = Build();
        pool.NextProbe = new FfprobeResult(TimeSpan.FromSeconds(123), null, null, Array.Empty<FfprobeStream>());

        var result = await task.RunAsync("/some/file.mp4", CancellationToken.None);

        Assert.Equal(123, result.DurationSeconds);
        Assert.NotNull(result.Json);
    }

    [Fact]
    public async Task RunAsync_ReturnsNullDuration_WhenProbeDurationIsNull()
    {
        var (task, pool) = Build();
        pool.NextProbe = new FfprobeResult(null, null, null, Array.Empty<FfprobeStream>());

        var result = await task.RunAsync("/some/file.mp4", CancellationToken.None);

        Assert.Null(result.DurationSeconds);
        Assert.Null(result.Json);
    }

    [Fact]
    public async Task RunAsync_ThrowsArgumentException_WhenInputPathIsWhitespace()
    {
        var (task, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            task.RunAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_HonoursCancellation()
    {
        var (task, pool) = Build();
        pool.CancelOnProbe = true;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.RunAsync("/some/file.mp4", cts.Token));
    }

    private sealed class StubPool : IFfmpegPool
    {
        public FfprobeResult? NextProbe { get; set; }
        public bool CancelOnProbe { get; set; }

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

        public Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(NextProbe ?? throw new InvalidOperationException("No probe queued"));
        }

        public Task<FfmpegProcessHandle> RunAsync(
            FfmpegInvocation invocation,
            IProgress<FfmpegProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<FfmpegProcessSnapshot> List()
        {
            return Array.Empty<FfmpegProcessSnapshot>();
        }

        public FfmpegProcessSnapshot? Get(Guid id)
        {
            return null;
        }

        public Task<bool> CancelAsync(Guid processId, CancellationToken ct = default)
        {
            return Task.FromResult(false);
        }
    }
}
