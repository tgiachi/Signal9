using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class JellyfinTagsTaskTests
{
    private static (JellyfinTagsTask Task, StubJellyfin Jellyfin, StubTagAccess Tags, StubMediaTagAccess MediaTags) Build()
    {
        var jellyfin = new StubJellyfin();
        var tags = new StubTagAccess();
        var mediaTags = new StubMediaTagAccess();
        var config = new PipelineConfig();
        var task = new JellyfinTagsTask(jellyfin, tags, mediaTags, config);

        return (task, jellyfin, tags, mediaTags);
    }

    [Fact]
    public void Order_Is75()
    {
        var (task, _, _, _) = Build();

        Assert.Equal(75, task.Order);
    }

    [Fact]
    public async Task Execute_MovieJellyfinMedia_AssignsGenresAndTags()
    {
        var (task, jellyfin, tags, mediaTags) = Build();
        var media = NewMedia(ChannelMediaType.Movies, MediaSourceType.Jellyfin);
        jellyfin.NextTags = new JellyfinItemTags(
            new[] { "Drama", "Sci-Fi" },
            new[] { "Christmas", "Drama" }
        );

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Equal(3, tags.Rows.Count);
        Assert.Contains(tags.Rows, tag => tag.Name == "drama" && tag.Label == "Drama");
        Assert.Contains(tags.Rows, tag => tag.Name == "sci-fi" && tag.Label == "Sci-Fi");
        Assert.Contains(tags.Rows, tag => tag.Name == "christmas" && tag.Label == "Christmas");
        Assert.Equal(3, mediaTags.Rows.Count);
        Assert.All(mediaTags.Rows, join => Assert.Equal(media.Id, join.ChannelMediaId));
        Assert.Equal("jf-item-1", jellyfin.LastItemId);
    }

    [Fact]
    public async Task Execute_TvShowJellyfinMedia_AssignsTags()
    {
        var (task, jellyfin, tags, mediaTags) = Build();
        var media = NewMedia(ChannelMediaType.TvShow, MediaSourceType.Jellyfin);
        jellyfin.NextTags = new JellyfinItemTags(new[] { "Crime" }, new[] { "Pilot" });

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Equal(2, tags.Rows.Count);
        Assert.Contains(tags.Rows, tag => tag.Name == "crime" && tag.Label == "Crime");
        Assert.Contains(tags.Rows, tag => tag.Name == "pilot" && tag.Label == "Pilot");
        Assert.Equal(2, mediaTags.Rows.Count);
    }

    [Fact]
    public async Task Execute_LocalMedia_SkipsJellyfinLookup()
    {
        var (task, jellyfin, tags, mediaTags) = Build();
        var media = NewMedia(ChannelMediaType.Movies, MediaSourceType.LocalFile);

        await task.ExecuteAsync(NewContext(media), CancellationToken.None);

        Assert.Equal(0, jellyfin.CallCount);
        Assert.Empty(tags.Rows);
        Assert.Empty(mediaTags.Rows);
    }

    private static ChannelMediaEntity NewMedia(ChannelMediaType type, MediaSourceType sourceType)
    {
        return new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = type,
            SourceType = sourceType,
            SourceRef = "jf-item-1"
        };
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

    private sealed class StubJellyfin : IJellyfinService
    {
        public JellyfinItemTags NextTags { get; set; } = new(Array.Empty<string>(), Array.Empty<string>());
        public string? LastItemId { get; private set; }
        public int CallCount { get; private set; }

        public Task<JellyfinServerInfo> GetServerInfoAsync(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<JellyfinLibrarySummary>> ListLibrariesAsync(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<JellyfinItem?> GetItemAsync(string itemId, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<JellyfinItem>> ListItemsAsync(string libraryId, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<JellyfinItemTags> GetItemTagsAsync(string itemId, CancellationToken ct = default)
        {
            CallCount++;
            LastItemId = itemId;
            return Task.FromResult(NextTags);
        }

        public Task<IReadOnlyList<JellyfinPreviewImage>> GetPreviewImagesAsync(
            string itemId,
            int maxImages,
            CancellationToken ct = default
        )
        {
            throw new NotSupportedException();
        }
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
}
