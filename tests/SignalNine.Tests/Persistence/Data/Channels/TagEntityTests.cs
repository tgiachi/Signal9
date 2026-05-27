using FreeSql;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;

namespace SignalNine.Tests.Persistence.Data.Channels;

public class TagEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;

    public TagEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<TagEntity>();
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
    public void Roundtrip_PersistsAndReads()
    {
        IDataAccess<TagEntity> dataAccess = new FreeSqlDataAccess<TagEntity>(_freeSql);
        var tag = new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = "sci-fi",
            Label = "Sci-Fi"
        };

        dataAccess.Insert(tag);

        var fetched = dataAccess.GetByKey(tag.Id);

        Assert.NotNull(fetched);
        Assert.Equal("sci-fi", fetched!.Name);
        Assert.Equal("Sci-Fi", fetched.Label);
    }

    [Fact]
    public void Name_UniqueConstraintRejectsDuplicate()
    {
        IDataAccess<TagEntity> dataAccess = new FreeSqlDataAccess<TagEntity>(_freeSql);

        dataAccess.Insert(new TagEntity { Id = Guid.NewGuid(), Name = "action" });

        Assert.ThrowsAny<Exception>(
            () => dataAccess.Insert(new TagEntity { Id = Guid.NewGuid(), Name = "action" })
        );
    }
}
