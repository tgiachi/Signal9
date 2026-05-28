using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Core.Types;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class ExtractPreviewsTaskTests : IDisposable
{
    private readonly string _tempBase;

    public ExtractPreviewsTaskTests()
    {
        _tempBase = Path.Combine(Path.GetTempPath(), $"signal9-extract-previews-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempBase);
    }

    [Fact]
    public async Task RunAsync_WithDurationHint_ReturnsThumbnailFilenames()
    {
        var pool = new StubPool(count: 3);
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-hint");

        var result = await task.RunAsync(inputPath, outputDir, count: 3, durationSecondsHint: 120, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, pool.ProbeCallCount);
        foreach (var name in result)
        {
            Assert.True(File.Exists(Path.Combine(outputDir, name)), $"Expected file {name} to exist in outputDir.");
        }
    }

    [Fact]
    public async Task RunAsync_WithoutDurationHint_CallsProbeFirst()
    {
        var pool = new StubPool(count: 2);
        pool.NextProbeDuration = TimeSpan.FromSeconds(90);
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-probe");

        var result = await task.RunAsync(inputPath, outputDir, count: 2, durationSecondsHint: null, CancellationToken.None);

        Assert.Equal(1, pool.ProbeCallCount);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task RunAsync_WithoutHintAndProbeReturnsNullDuration_ReturnsEmpty()
    {
        var pool = new StubPool(count: 0);
        pool.NextProbeDuration = null;
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-probe-fail");

        var result = await task.RunAsync(inputPath, outputDir, count: 3, durationSecondsHint: null, CancellationToken.None);

        Assert.Equal(1, pool.ProbeCallCount);
        Assert.Empty(result);
        Assert.Null(pool.LastInvocation);
    }

    [Fact]
    public async Task RunAsync_ExistingOutputDir_IsWipedBeforeRun()
    {
        var pool = new StubPool(count: 1);
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-wipe");
        Directory.CreateDirectory(outputDir);
        var staleFile = Path.Combine(outputDir, "stale.jpg");
        File.WriteAllText(staleFile, "old content");

        await task.RunAsync(inputPath, outputDir, count: 1, durationSecondsHint: 60, CancellationToken.None);

        Assert.False(File.Exists(staleFile));
        Assert.True(Directory.Exists(outputDir));
    }

    [Fact]
    public async Task RunAsync_CountZero_ReturnsEmptyWithoutInvokingFfmpeg()
    {
        var pool = new StubPool(count: 0);
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-zero");

        var result = await task.RunAsync(inputPath, outputDir, count: 0, durationSecondsHint: 60, CancellationToken.None);

        Assert.Empty(result);
        Assert.Null(pool.LastInvocation);
        Assert.Equal(0, pool.ProbeCallCount);
    }

    [Fact]
    public async Task RunAsync_FfmpegFails_ThrowsFfmpegExecutionException()
    {
        var pool = new StubPool(count: 0);
        pool.NextRunStatus = FfmpegProcessStatusType.Failed;
        pool.NextExitCode = 1;
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-fail");

        await Assert.ThrowsAsync<FfmpegExecutionException>(
            () => task.RunAsync(inputPath, outputDir, count: 2, durationSecondsHint: 60, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RunAsync_ResultFilesAreReturnedAsSortedBasenames()
    {
        var pool = new StubPool(count: 5);
        var task = new ExtractPreviewsTask(pool);
        var inputPath = Path.Combine(_tempBase, "input.mp4");
        File.WriteAllBytes(inputPath, Array.Empty<byte>());
        var outputDir = Path.Combine(_tempBase, "out-sorted");

        var result = await task.RunAsync(inputPath, outputDir, count: 5, durationSecondsHint: 300, CancellationToken.None);

        Assert.Equal(5, result.Count);
        var sorted = result.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, result);
        foreach (var name in result)
        {
            Assert.DoesNotContain(Path.DirectorySeparatorChar, name);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempBase))
        {
            try
            {
                Directory.Delete(_tempBase, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
        GC.SuppressFinalize(this);
    }

    private sealed class StubPool : IFfmpegPool
    {
        private readonly int _thumbCount;

        public TimeSpan? NextProbeDuration { get; set; } = TimeSpan.FromSeconds(60);
        public int ProbeCallCount { get; private set; }
        public FfmpegInvocation? LastInvocation { get; private set; }
        public FfmpegProcessStatusType NextRunStatus { get; set; } = FfmpegProcessStatusType.Completed;
        public int? NextExitCode { get; set; } = 0;

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

        public StubPool(int count)
        {
            _thumbCount = count;
        }

        public Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
        {
            ProbeCallCount++;
            return Task.FromResult(new FfprobeResult(NextProbeDuration, null, null, Array.Empty<FfprobeStream>()));
        }

        public Task<FfmpegProcessHandle> RunAsync(
            FfmpegInvocation invocation,
            IProgress<FfmpegProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
            LastInvocation = invocation;

            if (NextRunStatus == FfmpegProcessStatusType.Completed && _thumbCount > 0)
            {
                // Determine outputDir from the pattern argument (last arg contains thumb-%03d.jpg)
                var patternArg = invocation.Arguments[^1];
                var dir = Path.GetDirectoryName(patternArg);
                if (dir is not null && Directory.Exists(dir))
                {
                    for (var i = 1; i <= _thumbCount; i++)
                    {
                        var fileName = $"thumb-{i:D3}.jpg";
                        File.WriteAllBytes(Path.Combine(dir, fileName), Array.Empty<byte>());
                    }
                }
            }

            var snapshot = new FfmpegProcessSnapshot(
                Guid.NewGuid(),
                1,
                invocation.Executable,
                invocation.Arguments,
                NextRunStatus,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                NextExitCode,
                null,
                Array.Empty<string>(),
                null
            );
            var completion = Task.FromResult(snapshot);
            return Task.FromResult(new FfmpegProcessHandle(snapshot.Id, completion, () => Task.FromResult(false)));
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
