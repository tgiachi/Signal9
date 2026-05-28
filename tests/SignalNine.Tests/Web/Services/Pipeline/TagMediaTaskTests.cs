using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class TagMediaTaskTests
{
    private static (TagMediaTask Task, StubTagAccess Tags, StubMediaTagAccess MediaTags, PipelineConfig Config) Build()
    {
        var tags = new StubTagAccess();
        var mediaTags = new StubMediaTagAccess();
        var config = new PipelineConfig();
        var task = new TagMediaTask(tags, mediaTags, config);

        return (task, tags, mediaTags, config);
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
    public void Order_Is50()
    {
        var (task, _, _, _) = Build();

        Assert.Equal(50, task.Order);
    }

    [Fact]
    public void IsEnabled_UsesConfig()
    {
        var (task, _, _, config) = Build();

        Assert.True(task.IsEnabled);

        config.Tasks.Tagger.Enabled = false;

        Assert.False(task.IsEnabled);
    }

    [Fact]
    public async Task Execute_CommercialMedia_AddsCommercialAndAdvTags()
    {
        var (task, tags, mediaTags, _) = Build();
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Commercial
        };

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Contains(tags.Rows, tag => tag.Name == "commercials" && tag.Label == "Commercials");
        Assert.Contains(tags.Rows, tag => tag.Name == "adv" && tag.Label == "Advertising");
        Assert.Equal(2, tags.Rows.Count);
        Assert.Equal(2, mediaTags.Rows.Count);
        Assert.All(mediaTags.Rows, join => Assert.Equal(media.Id, join.ChannelMediaId));
    }

    [Fact]
    public async Task Execute_ExistingTagAndJoin_DoesNotDuplicate()
    {
        var (task, tags, mediaTags, _) = Build();
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Commercial
        };
        var commercials = tags.Insert(
            new TagEntity
            {
                Id = Guid.NewGuid(),
                Name = "commercials",
                Label = "Commercials"
            }
        );
        mediaTags.Insert(
            new ChannelMediaTagEntity
            {
                Id = Guid.NewGuid(),
                ChannelMediaId = media.Id,
                TagId = commercials.Id
            }
        );

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);
        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Single(tags.Rows, tag => tag.Name == "commercials");
        Assert.Single(tags.Rows, tag => tag.Name == "adv");
        Assert.Single(mediaTags.Rows, join => join.TagId == commercials.Id);
        Assert.Equal(2, tags.Rows.Count);
        Assert.Equal(2, mediaTags.Rows.Count);
    }

    [Fact]
    public async Task Execute_MoviesMedia_AddsMoviesTag()
    {
        var (task, tags, mediaTags, _) = Build();
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Movies
        };

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        var tag = Assert.Single(tags.Rows);
        Assert.Equal("movies", tag.Name);
        Assert.Equal("Movies", tag.Label);
        var join = Assert.Single(mediaTags.Rows);
        Assert.Equal(media.Id, join.ChannelMediaId);
        Assert.Equal(tag.Id, join.TagId);
    }

    private sealed class StubTagAccess : IDataAccess<TagEntity>
    {
        private readonly List<TagEntity> _rows = new();

        public IReadOnlyList<TagEntity> Rows => _rows;

        public TagEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(row => row.Id.Equals(key));
        }

        public IReadOnlyList<TagEntity> List()
        {
            return _rows;
        }

        public TagEntity Insert(TagEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }

        public int Update(TagEntity entity)
        {
            return 1;
        }

        public int Delete(object key)
        {
            return _rows.RemoveAll(row => row.Id.Equals(key));
        }
    }

    private sealed class StubMediaTagAccess : IDataAccess<ChannelMediaTagEntity>
    {
        private readonly List<ChannelMediaTagEntity> _rows = new();

        public IReadOnlyList<ChannelMediaTagEntity> Rows => _rows;

        public ChannelMediaTagEntity? GetByKey(object key)
        {
            return _rows.FirstOrDefault(row => row.Id.Equals(key));
        }

        public IReadOnlyList<ChannelMediaTagEntity> List()
        {
            return _rows;
        }

        public ChannelMediaTagEntity Insert(ChannelMediaTagEntity entity)
        {
            _rows.Add(entity);
            return entity;
        }

        public int Update(ChannelMediaTagEntity entity)
        {
            return 1;
        }

        public int Delete(object key)
        {
            return _rows.RemoveAll(row => row.Id.Equals(key));
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
