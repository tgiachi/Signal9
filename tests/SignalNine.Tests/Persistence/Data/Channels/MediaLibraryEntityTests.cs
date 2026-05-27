using FreeSql;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Persistence.Types;

namespace SignalNine.Tests.Persistence.Data.Channels;

public class MediaLibraryEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<MediaLibraryEntity> _dataAccess;

    public MediaLibraryEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<MediaLibraryEntity>();
        _dataAccess = new FreeSqlDataAccess<MediaLibraryEntity>(_freeSql);
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Roundtrip_PersistsAndReads()
    {
        var entity = new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Home Movies",
            Description = "Movies library on Jellyfin",
            DefaultMediaType = ChannelMediaType.Movies,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "jf-lib-001",
            IsActive = true,
            LastScannedAt = null
        };

        _dataAccess.Insert(entity);
        var fetched = _dataAccess.GetByKey(entity.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Home Movies", fetched!.Name);
        Assert.Equal(ChannelMediaType.Movies, fetched.DefaultMediaType);
        Assert.Equal(MediaSourceType.Jellyfin, fetched.SourceType);
        Assert.Equal("jf-lib-001", fetched.SourceRef);
        Assert.Null(fetched.LastScannedAt);
    }

    [Fact]
    public void Unique_SourceTypeAndRef_RejectsDuplicate()
    {
        _dataAccess.Insert(
            new MediaLibraryEntity
            {
                Id = Guid.NewGuid(),
                Name = "First",
                DefaultMediaType = ChannelMediaType.Movies,
                SourceType = MediaSourceType.Jellyfin,
                SourceRef = "jf-lib-dup"
            }
        );

        Assert.ThrowsAny<Exception>(
            () => _dataAccess.Insert(
                new MediaLibraryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Second",
                    DefaultMediaType = ChannelMediaType.Bumper,
                    SourceType = MediaSourceType.Jellyfin,
                    SourceRef = "jf-lib-dup"
                }
            )
        );
    }

    [Fact]
    public void Unique_AllowsSameRefAcrossDifferentSourceTypes()
    {
        _dataAccess.Insert(
            new MediaLibraryEntity
            {
                Id = Guid.NewGuid(),
                Name = "From Jellyfin",
                DefaultMediaType = ChannelMediaType.Movies,
                SourceType = MediaSourceType.Jellyfin,
                SourceRef = "same-ref"
            }
        );

        var exception = Record.Exception(
            () => _dataAccess.Insert(
                new MediaLibraryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "From URL",
                    DefaultMediaType = ChannelMediaType.Movies,
                    SourceType = MediaSourceType.Url,
                    SourceRef = "same-ref"
                }
            )
        );

        Assert.Null(exception);
    }
}
