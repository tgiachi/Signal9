using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class ProbeMediaTaskTests
{
    private static (ProbeMediaTask Task, StubPool Pool, StubMediaAccess Media, PipelineConfig Config) Build(
        bool overwrite = false,
        bool allowJellyfinStreamProbe = false
    )
    {
        var pool = new StubPool();
        var media = new StubMediaAccess();
        var config = new PipelineConfig
        {
            Tasks =
            {
                Probe =
                {
                    OverwriteExisting = overwrite,
                    AllowJellyfinStreamProbe = allowJellyfinStreamProbe
                }
            }
        };
        var task = new ProbeMediaTask(pool, media, config);
        return (task, pool, media, config);
    }

    private static PipelineContext NewContext(ChannelMediaEntity media)
    {
        return new PipelineContext(
            media,
            new MediaLibraryEntity { Id = Guid.NewGuid(), Name = "L", IsActive = true, SourceRef = "/x" },
            "/some/file.mp4",
            new JobExecutionContext(Guid.NewGuid(), "{}", Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}"), new InMemoryJobBus())
        );
    }

    [Fact]
    public void Order_Is100()
    {
        var (task, _, _, _) = Build();
        Assert.Equal(100, task.Order);
    }

    [Fact]
    public void IsEnabled_DefaultsTrue()
    {
        var (task, _, _, _) = Build();
        Assert.True(task.IsEnabled);
    }

    [Fact]
    public async Task Execute_PopulatesDurationFromProbe()
    {
        var (task, pool, media, _) = Build();
        pool.NextProbe = new FfprobeResult(TimeSpan.FromSeconds(123), null, null, Array.Empty<FfprobeStream>());

        var entity = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(123, entity.DurationSeconds);
        Assert.Single(media.Updated);
    }

    [Fact]
    public async Task Execute_SkipsWhenAlreadyPopulated_AndOverwriteFalse()
    {
        var (task, pool, media, _) = Build(overwrite: false);

        var entity = new ChannelMediaEntity
        {
            DurationSeconds = 99,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(99, entity.DurationSeconds);
        Assert.Empty(media.Updated);
        Assert.Equal(0, pool.ProbeCallCount);
    }

    [Fact]
    public async Task Execute_OverridesWhenOverwriteTrue()
    {
        var (task, pool, media, _) = Build(overwrite: true);
        pool.NextProbe = new FfprobeResult(TimeSpan.FromSeconds(500), null, null, Array.Empty<FfprobeStream>());

        var entity = new ChannelMediaEntity
        {
            DurationSeconds = 99,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(500, entity.DurationSeconds);
        Assert.Single(media.Updated);
    }

    [Fact]
    public async Task Execute_NullProbeDuration_LeavesMediaUnchanged()
    {
        var (task, pool, media, _) = Build();
        pool.NextProbe = new FfprobeResult(null, null, null, Array.Empty<FfprobeStream>());

        var entity = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.LocalFile
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Null(entity.DurationSeconds);
        Assert.Empty(media.Updated);
    }

    [Fact]
    public async Task Execute_JellyfinWithoutDuration_AndStreamProbeDisabled_SkipsProbe()
    {
        var (task, pool, media, _) = Build();
        var entity = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-1"
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, pool.ProbeCallCount);
        Assert.Empty(media.Updated);
    }

    [Fact]
    public async Task Execute_JellyfinWithoutDuration_AndStreamProbeEnabled_ProbesStream()
    {
        var (task, pool, media, _) = Build(allowJellyfinStreamProbe: true);
        pool.NextProbe = new FfprobeResult(TimeSpan.FromSeconds(80), null, null, Array.Empty<FfprobeStream>());
        var entity = new ChannelMediaEntity
        {
            DurationSeconds = null,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-1"
        };
        var ctx = NewContext(entity);

        await task.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(1, pool.ProbeCallCount);
        Assert.Equal(80, entity.DurationSeconds);
        Assert.Single(media.Updated);
    }

    private sealed class StubPool : IFfmpegPool
    {
        public FfprobeResult? NextProbe { get; set; }
        public int ProbeCallCount { get; private set; }

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

        public Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
        {
            ProbeCallCount++;
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

    private sealed class StubMediaAccess : IDataAccess<ChannelMediaEntity>
    {
        private readonly List<ChannelMediaEntity> _rows = new();
        public List<ChannelMediaEntity> Updated { get; } = new();

        public void Add(ChannelMediaEntity e)
        {
            _rows.Add(e);
        }

        public ChannelMediaEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(r => r.Id.Equals(key));
        }

        public IReadOnlyList<ChannelMediaEntity> List()
        {
            return _rows;
        }

        public ChannelMediaEntity Insert(ChannelMediaEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }

        public int Update(ChannelMediaEntity entity)
        {
            Updated.Add(entity);
            return 1;
        }

        public int Delete(object key)
        {
            return _rows.RemoveAll(r => r.Id.Equals(key));
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
