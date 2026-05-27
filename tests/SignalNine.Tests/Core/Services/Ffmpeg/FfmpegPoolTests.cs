using System.Collections.Concurrent;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services.Ffmpeg;

public class FfmpegPoolTests
{
    private static (FfmpegPool Pool, FakeProcessLauncher Launcher) Build(int maxConcurrent = 2, int outputLines = 50, int retention = 10)
    {
        var launcher = new FakeProcessLauncher();
        var config = new FfmpegPoolConfig
        {
            MaxConcurrent = maxConcurrent,
            OutputBufferLines = outputLines,
            RegistryRetention = retention,
            KillGraceSeconds = 1,
            FfmpegPath = "ffmpeg",
            FfprobePath = "ffprobe"
        };
        var pool = new FfmpegPool(launcher, config);
        return (pool, launcher);
    }

    [Fact]
    public async Task RunAsync_Happy_TransitionsToCompleted()
    {
        var (pool, launcher) = Build();
        var handle = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "-version" }));

        await Task.Delay(50);
        var fake = launcher.Started.Single();
        fake.Exit(0);

        var snapshot = await handle.Completion;

        Assert.Equal(FfmpegProcessStatusType.Completed, snapshot.Status);
        Assert.Equal(0, snapshot.ExitCode);
        Assert.NotNull(snapshot.EndedAt);
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_TransitionsToFailed()
    {
        var (pool, launcher) = Build();
        var handle = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "-fail" }));
        await Task.Delay(50);

        var fake = launcher.Started.Single();
        fake.EmitStderr("error: bad arg");
        fake.Exit(2);

        var snapshot = await handle.Completion;
        Assert.Equal(FfmpegProcessStatusType.Failed, snapshot.Status);
        Assert.Equal(2, snapshot.ExitCode);
        Assert.Contains("error: bad arg", snapshot.RecentOutputLines);
    }

    [Fact]
    public async Task RunAsync_ConcurrencyCapped()
    {
        var (pool, launcher) = Build(maxConcurrent: 2);
        var h1 = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "1" }));
        var h2 = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "2" }));
        var h3 = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "3" }));

        await Task.Delay(100);

        Assert.Equal(2, launcher.Started.Count);
        var snapshots = pool.List();
        Assert.Equal(3, snapshots.Count);
        Assert.Equal(FfmpegProcessStatusType.Queued, snapshots.Single(s => s.Id == h3.Id).Status);

        foreach (var p in launcher.Started)
        {
            p.Exit(0);
        }
        await h1.Completion;
        await h2.Completion;

        await Task.Delay(100);
        Assert.Equal(3, launcher.Started.Count);

        launcher.Started.Last().Exit(0);
        await h3.Completion;
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsProcessAndTransitionsToCanceled()
    {
        var (pool, launcher) = Build();
        using var cts = new CancellationTokenSource();
        var handle = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "x" }), ct: cts.Token);
        await Task.Delay(50);
        var fake = launcher.Started.Single();

        cts.Cancel();
        await Task.Delay(50);
        Assert.True(fake.Killed);
        fake.Exit(143);

        var snapshot = await handle.Completion;
        Assert.Equal(FfmpegProcessStatusType.Canceled, snapshot.Status);
    }

    [Fact]
    public async Task RunAsync_OutputBuffer_CapsAtConfiguredSize()
    {
        var (pool, launcher) = Build(outputLines: 5);
        var handle = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "x" }));
        await Task.Delay(50);

        var fake = launcher.Started.Single();
        for (int i = 0; i < 20; i++)
        {
            fake.EmitStderr($"line-{i}");
        }
        fake.Exit(0);

        var snapshot = await handle.Completion;
        Assert.Equal(5, snapshot.RecentOutputLines.Count);
        Assert.Contains("line-19", snapshot.RecentOutputLines);
        Assert.DoesNotContain("line-0", snapshot.RecentOutputLines);
    }

    [Fact]
    public async Task RunAsync_ProgressLine_FiresProgressCallback()
    {
        var (pool, launcher) = Build();
        var updates = new ConcurrentBag<FfmpegProgressUpdate>();
        var progress = new Progress<FfmpegProgressUpdate>(u => updates.Add(u));

        var handle = await pool.RunAsync(
            FfmpegInvocation.Custom("ffmpeg", new[] { "x" }),
            progress
        );
        await Task.Delay(50);

        var fake = launcher.Started.Single();
        fake.EmitStdout("frame=42");
        fake.EmitStdout("fps=24.5");
        fake.EmitStdout("out_time_us=2500000");
        fake.EmitStdout("total_size=10485760");
        fake.EmitStdout("speed=1.5x");
        fake.EmitStdout("progress=continue");
        await Task.Delay(100);
        fake.Exit(0);
        await handle.Completion;

        Assert.NotEmpty(updates);
        var update = updates.Last();
        Assert.Equal(42, update.FrameCount);
        Assert.Equal(24.5, update.Fps);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), update.OutTime);
        Assert.Equal(1.5, update.Speed);
        Assert.Equal(10485760, update.BytesProcessed);
    }

    [Fact]
    public async Task ProbeAsync_NonZeroExit_ThrowsExecutionException()
    {
        var (pool, launcher) = Build();

        launcher.OnStart = p =>
        {
            p.EmitStderr("invalid input");
            Task.Run(async () => { await Task.Delay(20); p.Exit(1); });
        };

        await Assert.ThrowsAsync<FfmpegExecutionException>(() => pool.ProbeAsync("/missing.mp4"));
    }

    [Fact]
    public async Task ProbeAsync_Happy_ParsesJson()
    {
        var (pool, launcher) = Build();
        const string json = """{"streams":[],"format":{"format_name":"mp4","duration":"42.000","size":"100"}}""";

        launcher.OnStart = p =>
        {
            foreach (var line in json.Split('\n'))
            {
                p.EmitStdout(line);
            }
            Task.Run(async () => { await Task.Delay(20); p.Exit(0); });
        };

        var result = await pool.ProbeAsync("/some.mp4");
        Assert.Equal(TimeSpan.FromSeconds(42), result.Duration);
        Assert.Equal(100L, result.SizeBytes);
        Assert.Equal("mp4", result.FormatName);
    }

    [Fact]
    public async Task ProcessChanged_FiresOnEveryTransition()
    {
        var (pool, launcher) = Build();
        var statuses = new ConcurrentBag<FfmpegProcessStatusType>();
        pool.ProcessChanged += (_, s) => statuses.Add(s.Status);

        var handle = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "x" }));
        await Task.Delay(50);
        launcher.Started.Single().Exit(0);
        await handle.Completion;

        Assert.Contains(FfmpegProcessStatusType.Queued, statuses);
        Assert.Contains(FfmpegProcessStatusType.Running, statuses);
        Assert.Contains(FfmpegProcessStatusType.Completed, statuses);
    }

    [Fact]
    public async Task Registry_RespectsRetention()
    {
        var (pool, launcher) = Build(retention: 10);

        for (int i = 0; i < 15; i++)
        {
            var h = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { $"run-{i}" }));
            await Task.Delay(20);
            var fake = launcher.Started.Last();
            fake.Exit(0);
            await h.Completion;
        }

        Assert.True(pool.List().Count <= 10);
    }

    [Fact]
    public async Task CancelAsync_AfterTerminal_ReturnsFalse()
    {
        var (pool, launcher) = Build();
        var h = await pool.RunAsync(FfmpegInvocation.Custom("ffmpeg", new[] { "x" }));
        await Task.Delay(50);
        launcher.Started.Single().Exit(0);
        await h.Completion;

        var result = await pool.CancelAsync(h.Id);
        Assert.False(result);
    }
}
