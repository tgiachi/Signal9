using FreeSql;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Persistence.Types;

namespace SignalNine.Tests.Persistence.Data.Channels;

public class ChannelMediaEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<ChannelMediaEntity> _dataAccess;

    public ChannelMediaEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<ChannelMediaEntity>();
        _dataAccess = new FreeSqlDataAccess<ChannelMediaEntity>(_freeSql);
    }

    public void Dispose()
    {
        _freeSql.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Roundtrip_Commercial_PersistsAndReads()
    {
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Commercial,
            Title = "Brand X — Spring Campaign",
            DurationSeconds = 30,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-001",
            CommercialAdvertiser = "Brand X",
            CommercialCampaign = "Spring 2026"
        };

        _dataAccess.Insert(media);
        var fetched = _dataAccess.GetByKey(media.Id);

        Assert.NotNull(fetched);
        Assert.Equal(ChannelMediaType.Commercial, fetched!.Type);
        Assert.Equal("Brand X", fetched.CommercialAdvertiser);
        Assert.Equal("Spring 2026", fetched.CommercialCampaign);
        Assert.Null(fetched.MovieReleaseYear);
        Assert.Null(fetched.TvSeriesName);
    }

    [Fact]
    public void Roundtrip_Movies_PersistsAndReads()
    {
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Movies,
            Title = "Die Hard",
            DurationSeconds = 7320,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-002",
            MovieReleaseYear = 1988,
            MovieDirector = "John McTiernan"
        };

        _dataAccess.Insert(media);
        var fetched = _dataAccess.GetByKey(media.Id);

        Assert.NotNull(fetched);
        Assert.Equal(ChannelMediaType.Movies, fetched!.Type);
        Assert.Equal(1988, fetched.MovieReleaseYear);
        Assert.Equal("John McTiernan", fetched.MovieDirector);
        Assert.Null(fetched.TvSeriesName);
        Assert.Null(fetched.CommercialAdvertiser);
    }

    [Fact]
    public void Roundtrip_TvShow_PersistsAndReads()
    {
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.TvShow,
            Title = "Pilot",
            DurationSeconds = 2700,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-item-003",
            TvSeriesName = "Breaking Bad",
            TvSeason = 1,
            TvEpisode = 1
        };

        _dataAccess.Insert(media);
        var fetched = _dataAccess.GetByKey(media.Id);

        Assert.NotNull(fetched);
        Assert.Equal(ChannelMediaType.TvShow, fetched!.Type);
        Assert.Equal("Breaking Bad", fetched.TvSeriesName);
        Assert.Equal(1, fetched.TvSeason);
        Assert.Equal(1, fetched.TvEpisode);
        Assert.Null(fetched.MovieReleaseYear);
    }

    [Fact]
    public void Roundtrip_Bumper_PersistsWithNoTypeSpecificFields()
    {
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Bumper,
            Title = "Channel ID 5s",
            DurationSeconds = 5,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "bumpers/id-5s.mp4"
        };

        _dataAccess.Insert(media);
        var fetched = _dataAccess.GetByKey(media.Id);

        Assert.NotNull(fetched);
        Assert.Equal(ChannelMediaType.Bumper, fetched!.Type);
        Assert.Equal(MediaSourceType.LocalFile, fetched.SourceType);
        Assert.Null(fetched.MovieReleaseYear);
        Assert.Null(fetched.TvSeriesName);
        Assert.Null(fetched.CommercialAdvertiser);
        Assert.Null(fetched.InformationEdition);
    }

    [Fact]
    public void Roundtrip_Information_PersistsEditionField()
    {
        var media = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = ChannelMediaType.Information,
            Title = "Morning News",
            DurationSeconds = 1800,
            SourceType = MediaSourceType.Url,
            SourceRef = "https://stream.example.com/news/morning.m3u8",
            InformationEdition = "Morning"
        };

        _dataAccess.Insert(media);
        var fetched = _dataAccess.GetByKey(media.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Morning", fetched!.InformationEdition);
        Assert.Equal(MediaSourceType.Url, fetched.SourceType);
    }

    [Fact]
    public void Source_PolymorphicReferenceRoundtrip_ForAllSourceTypes()
    {
        foreach (var sourceType in Enum.GetValues<MediaSourceType>())
        {
            var media = new ChannelMediaEntity
            {
                Id = Guid.NewGuid(),
                Type = ChannelMediaType.Bumper,
                Title = $"Source-{sourceType}",
                SourceType = sourceType,
                SourceRef = $"ref-{sourceType}"
            };

            _dataAccess.Insert(media);
            var fetched = _dataAccess.GetByKey(media.Id);

            Assert.NotNull(fetched);
            Assert.Equal(sourceType, fetched!.SourceType);
            Assert.Equal($"ref-{sourceType}", fetched.SourceRef);
        }
    }
}
