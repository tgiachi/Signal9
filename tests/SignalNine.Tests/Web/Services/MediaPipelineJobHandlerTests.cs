using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Core.Types;
using SignalNine.Jobs.Services;
using SignalNine.Jobs.Services.Pipeline;
using System.Text.Json;

namespace SignalNine.Tests.Web.Services;

public class MediaPipelineJobHandlerTests : IDisposable
{
    private readonly string _tempBase;

    public MediaPipelineJobHandlerTests()
    {
        _tempBase = Path.Combine(Path.GetTempPath(), $"pipeline-handler-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempBase);
    }

    private string CreateWorkDir()
    {
        var workDir = Path.Combine(_tempBase, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(workDir, "input"));
        return workDir;
    }

    private static string WriteInputFile(string workDir, string relativePath = "input/movie.mp4")
    {
        var inputPath = Path.Combine(workDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
        File.WriteAllText(inputPath, "fake-bytes");
        return inputPath;
    }

    private static JobExecutionContext NewContext(string workDir, Guid channelMediaId, string inputFile = "input/movie.mp4", int previewCount = 5)
    {
        var payload = JsonSerializer.Serialize(new MediaPipelinePayloadV2(channelMediaId, inputFile, previewCount));
        return new JobExecutionContext(Guid.NewGuid(), payload, workDir, new InMemoryJobBus());
    }

    private static (ProbeMediaTask Probe, ExtractPreviewsTask Extract, StubPool Pool) BuildTasks(
        TimeSpan? probeDuration = null,
        int thumbCount = 5)
    {
        var pool = new StubPool(thumbCount: thumbCount, probeDuration: probeDuration);
        var config = new PipelineConfig();
        var probe = new ProbeMediaTask(pool, config);
        var extract = new ExtractPreviewsTask(pool);
        return (probe, extract, pool);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsCorrectResult()
    {
        var workDir = CreateWorkDir();
        WriteInputFile(workDir);
        var channelMediaId = Guid.NewGuid();
        var (probe, extract, _) = BuildTasks(probeDuration: TimeSpan.FromSeconds(300), thumbCount: 5);
        var handler = new MediaPipelineJobHandler(probe, extract);
        var context = NewContext(workDir, channelMediaId, previewCount: 5);

        var result = (MediaPipelineResult)await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(channelMediaId, result.ChannelMediaId);
        Assert.Equal(300, result.DurationSeconds);
        Assert.Equal(5, result.PreviewFiles.Count);
        Assert.NotNull(result.ProbeJson);
    }

    [Fact]
    public async Task ExecuteAsync_MissingInputFile_ThrowsFileNotFoundException()
    {
        var workDir = CreateWorkDir();
        // Do NOT create the input file
        var channelMediaId = Guid.NewGuid();
        var (probe, extract, _) = BuildTasks(probeDuration: TimeSpan.FromSeconds(120), thumbCount: 3);
        var handler = new MediaPipelineJobHandler(probe, extract);
        var context = NewContext(workDir, channelMediaId);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            handler.ExecuteAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NullProbeDuration_ResultDurationSecondIsNull()
    {
        var workDir = CreateWorkDir();
        WriteInputFile(workDir);
        var channelMediaId = Guid.NewGuid();
        // probeDuration null => ProbeMediaTask returns null DurationSeconds, and ExtractPreviewsTask
        // will call probe again (pool still returns null) => returns empty list
        var (probe, extract, _) = BuildTasks(probeDuration: null, thumbCount: 0);
        var handler = new MediaPipelineJobHandler(probe, extract);
        var context = NewContext(workDir, channelMediaId, previewCount: 5);

        var result = (MediaPipelineResult)await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Null(result.DurationSeconds);
        Assert.Empty(result.PreviewFiles);
        Assert.Null(result.ProbeJson);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var workDir = CreateWorkDir();
        WriteInputFile(workDir);
        var channelMediaId = Guid.NewGuid();
        var (probe, extract, _) = BuildTasks(probeDuration: TimeSpan.FromSeconds(60), thumbCount: 5);
        var handler = new MediaPipelineJobHandler(probe, extract);
        var context = NewContext(workDir, channelMediaId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPayloadJson_ThrowsInvalidOperationException()
    {
        var workDir = CreateWorkDir();
        var (probe, extract, _) = BuildTasks();
        var handler = new MediaPipelineJobHandler(probe, extract);
        // "null" deserializes to null → throws InvalidOperationException("Empty pipeline payload.")
        var context = new JobExecutionContext(Guid.NewGuid(), "null", workDir, new InMemoryJobBus());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(context, CancellationToken.None));

        Assert.Contains("Empty pipeline payload.", ex.Message);
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
        private readonly TimeSpan? _probeDuration;

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

        public StubPool(int thumbCount, TimeSpan? probeDuration)
        {
            _thumbCount = thumbCount;
            _probeDuration = probeDuration;
        }

        public Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new FfprobeResult(_probeDuration, null, null, Array.Empty<FfprobeStream>()));
        }

        public Task<FfmpegProcessHandle> RunAsync(
            FfmpegInvocation invocation,
            IProgress<FfmpegProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_thumbCount > 0)
            {
                var patternArg = invocation.Arguments[^1];
                var dir = Path.GetDirectoryName(patternArg);
                if (dir is not null && Directory.Exists(dir))
                {
                    for (var i = 1; i <= _thumbCount; i++)
                    {
                        File.WriteAllBytes(Path.Combine(dir, $"thumb-{i:D3}.jpg"), Array.Empty<byte>());
                    }
                }
            }

            var snapshot = new FfmpegProcessSnapshot(
                Guid.NewGuid(),
                1,
                invocation.Executable,
                invocation.Arguments,
                FfmpegProcessStatusType.Completed,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                0,
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
