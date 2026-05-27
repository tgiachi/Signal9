using FreeSql;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Tests.Support.Persistence;

namespace SignalNine.Tests.Persistence.Services;

public class FreeSqlDataAccessTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;

    public FreeSqlDataAccessTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<TestBroadcastItem>();
    }

    [Fact]
    public void Insert_Get_ReturnsPersistedEntity()
    {
        IDataAccess<TestBroadcastItem> dataAccess = new FreeSqlDataAccess<TestBroadcastItem>(_freeSql);
        var entity = new TestBroadcastItem
        {
            Id = "episode-1",
            Title = "Pilot",
            SortOrder = 1
        };

        dataAccess.Insert(entity);

        var persistedEntity = dataAccess.GetByKey(entity.Id);

        Assert.NotNull(persistedEntity);
        Assert.Equal(entity.Id, persistedEntity.Id);
        Assert.Equal(entity.Title, persistedEntity.Title);
        Assert.Equal(entity.SortOrder, persistedEntity.SortOrder);
    }

    [Fact]
    public void List_InsertedEntities_ReturnsAllEntities()
    {
        IDataAccess<TestBroadcastItem> dataAccess = new FreeSqlDataAccess<TestBroadcastItem>(_freeSql);

        dataAccess.Insert(new TestBroadcastItem { Id = "episode-1", Title = "Pilot", SortOrder = 1 });
        dataAccess.Insert(new TestBroadcastItem { Id = "episode-2", Title = "Finale", SortOrder = 2 });

        var entities = dataAccess.List();

        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, entity => entity.Id == "episode-1");
        Assert.Contains(entities, entity => entity.Id == "episode-2");
    }

    [Fact]
    public void Update_ExistingEntity_PersistsChanges()
    {
        IDataAccess<TestBroadcastItem> dataAccess = new FreeSqlDataAccess<TestBroadcastItem>(_freeSql);
        var entity = new TestBroadcastItem { Id = "episode-1", Title = "Pilot", SortOrder = 1 };
        dataAccess.Insert(entity);

        entity.Title = "Updated Pilot";
        var affectedRows = dataAccess.Update(entity);

        var persistedEntity = dataAccess.GetByKey(entity.Id);

        Assert.Equal(1, affectedRows);
        Assert.NotNull(persistedEntity);
        Assert.Equal("Updated Pilot", persistedEntity.Title);
    }

    [Fact]
    public void Delete_ExistingEntity_RemovesEntity()
    {
        IDataAccess<TestBroadcastItem> dataAccess = new FreeSqlDataAccess<TestBroadcastItem>(_freeSql);
        var entity = new TestBroadcastItem { Id = "episode-1", Title = "Pilot", SortOrder = 1 };
        dataAccess.Insert(entity);

        var affectedRows = dataAccess.Delete(entity.Id);

        Assert.Equal(1, affectedRows);
        Assert.Null(dataAccess.GetByKey(entity.Id));
    }

    public void Dispose()
    {
        if (_freeSql is IDisposable disposableFreeSql)
        {
            disposableFreeSql.Dispose();
        }

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        GC.SuppressFinalize(this);
    }
}
