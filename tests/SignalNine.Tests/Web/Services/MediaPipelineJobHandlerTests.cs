using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Interfaces;
using SignalNine.Jobs.Services;

namespace SignalNine.Tests.Web.Services;

public class MediaPipelineJobHandlerTests
{
    private static (
        MediaPipelineJobHandler Handler,
        StubMediaAccess Media,
        StubLibAccess Libs,
        StubResolver Resolver,
        List<RecordingTask> Tasks,
        StubJobs Jobs
    ) Build(params RecordingTask[] tasks)
    {
        var media = new StubMediaAccess();
        var libs = new StubLibAccess();
        var resolver = new StubResolver();
        var jobs = new StubJobs();
        var taskList = tasks.ToList();

        var services = new ServiceCollection();
        services.AddScoped<IDataAccess<ChannelMediaEntity>>(_ => media);
        services.AddScoped<IDataAccess<MediaLibraryEntity>>(_ => libs);
        services.AddScoped<IMediaPathResolver>(_ => resolver);
        services.AddScoped<IJobManager>(_ => jobs);
        foreach (var t in taskList)
        {
            var captured = t;
            services.AddScoped<IPipelineTask>(_ => captured);
        }
        var sp = services.BuildServiceProvider();

        var handler = new MediaPipelineJobHandler(sp.GetRequiredService<IServiceScopeFactory>());
        return (handler, media, libs, resolver, taskList, jobs);
    }

    private static JobExecutionContext NewContext(Guid mediaId)
    {
        var payload = JsonSerializer.Serialize(new MediaPipelinePayload(mediaId));
        var workDir = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        return new JobExecutionContext(Guid.NewGuid(), payload, workDir, new InMemoryJobBus());
    }

    private static MediaLibraryEntity NewLib()
    {
        return new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = "L",
            DefaultMediaType = ChannelMediaType.Movies,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "/m",
            IsActive = true
        };
    }

    private static ChannelMediaEntity NewMedia(Guid libraryId)
    {
        return new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Movies,
            Title = "T",
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "a.mp4",
            IsActive = true,
            MediaLibraryId = libraryId
        };
    }

    [Fact]
    public async Task Execute_OrdersTasksAscending()
    {
        var t100 = new RecordingTask("a", 100, true);
        var t50 = new RecordingTask("b", 50, true);
        var t200 = new RecordingTask("c", 200, true);

        var (handler, media, libs, _, _, _) = Build(t100, t50, t200);
        var lib = NewLib();
        var entity = NewMedia(lib.Id);
        libs.Add(lib);
        media.Add(entity);

        await handler.ExecuteAsync(NewContext(entity.Id), CancellationToken.None);

        Assert.True(t50.ExecutedAt < t100.ExecutedAt);
        Assert.True(t100.ExecutedAt < t200.ExecutedAt);
        Assert.True(t50.Executed && t100.Executed && t200.Executed);
    }

    [Fact]
    public async Task Execute_SkipsDisabledTasks()
    {
        var enabled = new RecordingTask("on", 10, true);
        var disabled = new RecordingTask("off", 20, false);

        var (handler, media, libs, _, _, _) = Build(enabled, disabled);
        var lib = NewLib();
        var entity = NewMedia(lib.Id);
        libs.Add(lib);
        media.Add(entity);

        await handler.ExecuteAsync(NewContext(entity.Id), CancellationToken.None);

        Assert.True(enabled.Executed);
        Assert.False(disabled.Executed);
    }

    [Fact]
    public async Task Execute_FailingTask_LoggedAndContinues()
    {
        var failing = new RecordingTask("bad", 10, true) { ThrowOnExecute = new InvalidOperationException("boom") };
        var next = new RecordingTask("good", 20, true);

        var (handler, media, libs, _, _, jobs) = Build(failing, next);
        var lib = NewLib();
        var entity = NewMedia(lib.Id);
        libs.Add(lib);
        media.Add(entity);

        await handler.ExecuteAsync(NewContext(entity.Id), CancellationToken.None);

        Assert.True(next.Executed);
        Assert.Single(jobs.Logs);
        Assert.Contains("bad", jobs.Logs[0]);
        Assert.Contains("boom", jobs.Logs[0]);
        Assert.Equal(JobLogLevelType.Warning, jobs.LogLevels[0]);
    }

    [Fact]
    public async Task Execute_MediaMissing_Throws()
    {
        var (handler, _, libs, _, _, _) = Build();
        var lib = NewLib();
        libs.Add(lib);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(NewContext(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Execute_LibraryMissing_Throws()
    {
        var (handler, media, _, _, _, _) = Build();
        var entity = NewMedia(Guid.NewGuid());
        media.Add(entity);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(NewContext(entity.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Execute_PathResolverThrows_BubblesUp()
    {
        var (handler, media, libs, resolver, _, _) = Build();
        var lib = NewLib();
        var entity = NewMedia(lib.Id);
        libs.Add(lib);
        media.Add(entity);
        resolver.ThrowMessage = "no path";

        await Assert.ThrowsAsync<MediaPathResolutionException>(() =>
            handler.ExecuteAsync(NewContext(entity.Id), CancellationToken.None));
    }

    private sealed class RecordingTask : IPipelineTask
    {
        private static int _counter;

        public string Name { get; }
        public int Order { get; }
        public bool IsEnabled { get; }
        public bool Executed { get; private set; }
        public int ExecutedAt { get; private set; }
        public Exception? ThrowOnExecute { get; set; }

        public RecordingTask(string name, int order, bool enabled)
        {
            Name = name;
            Order = order;
            IsEnabled = enabled;
        }

        public Task ExecuteAsync(PipelineContext context, CancellationToken ct)
        {
            Executed = true;
            ExecutedAt = Interlocked.Increment(ref _counter);
            if (ThrowOnExecute is not null)
            {
                throw ThrowOnExecute;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class StubResolver : IMediaPathResolver
    {
        public string? ThrowMessage { get; set; }

        public Task<string> ResolveAsync(ChannelMediaEntity media, MediaLibraryEntity library, CancellationToken ct)
        {
            if (ThrowMessage is not null)
            {
                throw new MediaPathResolutionException(ThrowMessage);
            }
            return Task.FromResult($"/resolved/{media.SourceRef}");
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

    private sealed class StubLibAccess : IDataAccess<MediaLibraryEntity>
    {
        private readonly List<MediaLibraryEntity> _rows = new();

        public void Add(MediaLibraryEntity e)
        {
            _rows.Add(e);
        }

        public MediaLibraryEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(r => r.Id.Equals(key));
        }

        public IReadOnlyList<MediaLibraryEntity> List()
        {
            return _rows;
        }

        public MediaLibraryEntity Insert(MediaLibraryEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }

        public int Update(MediaLibraryEntity entity)
        {
            return 1;
        }

        public int Delete(object key)
        {
            return _rows.RemoveAll(r => r.Id.Equals(key));
        }
    }

    private sealed class StubJobs : IJobManager
    {
        public List<string> Logs { get; } = new();
        public List<JobLogLevelType> LogLevels { get; } = new();

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
            Logs.Add(message);
            LogLevels.Add(level);
            return Task.CompletedTask;
        }

        public CancellationToken GetCancellationToken(Guid jobId)
        {
            return CancellationToken.None;
        }

        public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
            => DequeueAsync(cancellationToken);
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

        public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
            => DequeueAsync(cancellationToken);
    }
}
