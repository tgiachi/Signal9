using FreeSql;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;

namespace SignalNine.Tests.Persistence.Data.Channels;

public class ChannelMediaTagEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<ChannelMediaTagEntity> _dataAccess;

    public ChannelMediaTagEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<ChannelMediaTagEntity>();
        _dataAccess = new FreeSqlDataAccess<ChannelMediaTagEntity>(_freeSql);
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
    public void Roundtrip_PersistsAssociation()
    {
        var mediaId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var association = new ChannelMediaTagEntity
        {
            Id = Guid.NewGuid(),
            ChannelMediaId = mediaId,
            TagId = tagId
        };

        _dataAccess.Insert(association);
        var fetched = _dataAccess.GetByKey(association.Id);

        Assert.NotNull(fetched);
        Assert.Equal(mediaId, fetched!.ChannelMediaId);
        Assert.Equal(tagId, fetched.TagId);
    }

    [Fact]
    public void Composite_DuplicateAssociationRejected()
    {
        var mediaId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _dataAccess.Insert(
            new ChannelMediaTagEntity
            {
                Id = Guid.NewGuid(),
                ChannelMediaId = mediaId,
                TagId = tagId
            }
        );

        Assert.ThrowsAny<Exception>(
            () => _dataAccess.Insert(
                new ChannelMediaTagEntity
                {
                    Id = Guid.NewGuid(),
                    ChannelMediaId = mediaId,
                    TagId = tagId
                }
            )
        );
    }

    [Fact]
    public void Composite_DifferentMediaSameTag_IsAllowed()
    {
        var tagId = Guid.NewGuid();

        _dataAccess.Insert(
            new ChannelMediaTagEntity
            {
                Id = Guid.NewGuid(),
                ChannelMediaId = Guid.NewGuid(),
                TagId = tagId
            }
        );

        var exception = Record.Exception(
            () => _dataAccess.Insert(
                new ChannelMediaTagEntity
                {
                    Id = Guid.NewGuid(),
                    ChannelMediaId = Guid.NewGuid(),
                    TagId = tagId
                }
            )
        );

        Assert.Null(exception);
    }
}
