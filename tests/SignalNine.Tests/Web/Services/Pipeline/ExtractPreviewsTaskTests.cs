using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class ExtractPreviewsTaskTests : IDisposable
{
    private const string ResolvedPath = "/path/to/file.mp4";

    private readonly string _rootDir;

    public ExtractPreviewsTaskTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"signal9-preview-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        foreach (var name in Enum.GetNames<DirectoryType>())
        {
            Directory.CreateDirectory(Path.Combine(_rootDir, name));
        }
    }

    [Fact]
    public void Order_Is200()
    {
        var (task, _, _, _) = Build();
        Assert.Equal(200, task.Order);
    }

    [Fact]
    public async Task Execute_BuildsExtractThumbnailsInvocation()
    {
        var (task, pool, _, _) = Build();
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 120,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(pool.LastInvocation);
        Assert.Contains(ResolvedPath, pool.LastInvocation!.Arguments);
        Assert.Contains("-frames:v", pool.LastInvocation.Arguments);
    }

    [Fact]
    public async Task Execute_OutputDirContainsChannelMediaId()
    {
        var (task, pool, _, _) = Build();
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 60,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(pool.LastInvocation);
        var lastArg = pool.LastInvocation!.Arguments[^1];
        Assert.Contains(media.Id.ToString(), lastArg);
        Assert.Contains("thumb-%03d.jpg", lastArg);
    }

    [Fact]
    public async Task Execute_UsesConfiguredPreviewCount()
    {
        var (task, pool, _, config) = Build();
        config.Tasks.Preview.PreviewCount = 3;
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 120,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(pool.LastInvocation);
        var arguments = pool.LastInvocation!.Arguments.ToList();
        var framesIndex = arguments.IndexOf("-frames:v");
        Assert.True(framesIndex >= 0);
        Assert.Equal("3", arguments[framesIndex + 1]);
    }

    [Fact]
    public async Task Execute_CleansExistingDirBeforeRun()
    {
        var (task, _, directories, config) = Build();
        config.Tasks.Preview.OverwriteExisting = true;
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 60,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        var outputDir = Path.Combine(
            directories[DirectoryType.Assets],
            "previews",
            media.Id.ToString()
        );
        Directory.CreateDirectory(outputDir);
        var staleFile = Path.Combine(outputDir, "stale.jpg");
        File.WriteAllText(staleFile, "old");

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(Directory.Exists(outputDir));
        Assert.False(File.Exists(staleFile));
    }

    [Fact]
    public async Task Execute_ExistingPreview_AndOverwriteFalse_SkipsRun()
    {
        var (task, pool, directories, _) = Build();
        var media = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        var outputDir = Path.Combine(
            directories[DirectoryType.Assets],
            "previews",
            media.Id.ToString()
        );
        Directory.CreateDirectory(outputDir);
        var existingFile = Path.Combine(outputDir, "thumb-001.jpg");
        File.WriteAllText(existingFile, "existing");

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(File.Exists(existingFile));
        Assert.Equal(0, pool.ProbeCallCount);
        Assert.Null(pool.LastInvocation);
    }

    [Fact]
    public async Task Execute_NoDuration_AndProbeFallbackReturnsNull_SkipsRun()
    {
        var (task, pool, _, _) = Build();
        pool.NextProbeDuration = null;

        var media = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(1, pool.ProbeCallCount);
        Assert.Null(pool.LastInvocation);
    }

    [Fact]
    public async Task Execute_NoDuration_FallsBackToProbe()
    {
        var (task, pool, _, _) = Build();
        pool.NextProbeDuration = TimeSpan.FromSeconds(120);

        var media = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(1, pool.ProbeCallCount);
        Assert.NotNull(pool.LastInvocation);
    }

    [Fact]
    public async Task Execute_RunReturnsFailedSnapshot_Throws()
    {
        var (task, pool, _, _) = Build();
        pool.NextRunStatus = FfmpegProcessStatusType.Failed;
        pool.NextExitCode = 1;

        var media = new ChannelMediaEntity
        {
            DurationSeconds = 60,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(media);

        await Assert.ThrowsAsync<FfmpegExecutionException>(
            () => task.ExecuteAsync(ctx, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Execute_JellyfinMedia_AndStreamFallbackDisabled_SkipsRun()
    {
        var (task, pool, _, _) = Build();
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 60,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-1"
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, pool.ProbeCallCount);
        Assert.Null(pool.LastInvocation);
    }

    [Fact]
    public async Task Execute_JellyfinMedia_AndStreamFallbackEnabled_RunsExtraction()
    {
        var (task, pool, _, config) = Build();
        config.Tasks.Preview.AllowJellyfinStreamFallback = true;
        var media = new ChannelMediaEntity
        {
            DurationSeconds = 60,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-1"
        };
        var ctx = NewContext(media);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(pool.LastInvocation);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            try
            {
                Directory.Delete(_rootDir, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
        GC.SuppressFinalize(this);
    }

    private (ExtractPreviewsTask Task, StubPool Pool, DirectoriesConfig Directories, PipelineConfig Config) Build()
    {
        var pool = new StubPool();
        var directories = new DirectoriesConfig(_rootDir, Enum.GetNames<DirectoryType>());
        var config = new PipelineConfig();
        var task = new ExtractPreviewsTask(pool, directories, config);
        return (task, pool, directories, config);
    }

    private static PipelineContext NewContext(ChannelMediaEntity media)
    {
        return new PipelineContext(
            media,
            new MediaLibraryEntity { Id = Guid.NewGuid(), Name = "L", IsActive = true, SourceRef = "/x" },
            ResolvedPath,
            new JobExecutionContext(Guid.NewGuid(), "{}", new NoopJobManager())
        );
    }

    private sealed class StubPool : IFfmpegPool
    {
        public TimeSpan? NextProbeDuration { get; set; } = TimeSpan.FromSeconds(60);
        public int ProbeCallCount { get; private set; }
        public FfmpegInvocation? LastInvocation { get; private set; }
        public FfmpegProcessStatusType NextRunStatus { get; set; } = FfmpegProcessStatusType.Completed;
        public int? NextExitCode { get; set; } = 0;

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

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

    private sealed class NoopJobManager : IJobManager
    {
        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<JobSnapshot> List()
        {
            return Array.Empty<JobSnapshot>();
        }

        public JobSnapshot? GetById(Guid jobId)
        {
            return null;
        }

        public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
        {
            return Array.Empty<JobLogEntry>();
        }

        public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
            => DequeueAsync(cancellationToken);

        public Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public CancellationToken GetCancellationToken(Guid jobId)
        {
            return CancellationToken.None;
        }
    }
}
