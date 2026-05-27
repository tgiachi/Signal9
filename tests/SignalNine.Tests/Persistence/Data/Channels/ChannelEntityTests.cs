using FreeSql;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;

namespace SignalNine.Tests.Persistence.Data.Channels;

public class ChannelEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;

    public ChannelEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<ChannelEntity>();
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
        IDataAccess<ChannelEntity> dataAccess = new FreeSqlDataAccess<ChannelEntity>(_freeSql);
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var updatedAt = DateTime.UtcNow;

        var channel = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "Sport One",
            Slug = "sport-one",
            Description = "Live sports coverage",
            LogoUrl = "logos/sport-one.png",
            DisplayOrder = 1,
            IsActive = true,
            CommercialsEnabled = true,
            CommercialIntervalMinSeconds = 180,
            CommercialIntervalMaxSeconds = 420,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        dataAccess.Insert(channel);

        var fetched = dataAccess.GetByKey(channel.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Sport One", fetched!.Name);
        Assert.Equal("sport-one", fetched.Slug);
        Assert.Equal("Live sports coverage", fetched.Description);
        Assert.Equal("logos/sport-one.png", fetched.LogoUrl);
        Assert.Equal(1, fetched.DisplayOrder);
        Assert.True(fetched.IsActive);
        Assert.True(fetched.CommercialsEnabled);
        Assert.Equal(180, fetched.CommercialIntervalMinSeconds);
        Assert.Equal(420, fetched.CommercialIntervalMaxSeconds);
    }

    [Fact]
    public void Slug_UniqueConstraintRejectsDuplicate()
    {
        IDataAccess<ChannelEntity> dataAccess = new FreeSqlDataAccess<ChannelEntity>(_freeSql);

        dataAccess.Insert(
            new ChannelEntity
            {
                Id = Guid.NewGuid(),
                Name = "First",
                Slug = "duplicate-slug"
            }
        );

        Assert.ThrowsAny<Exception>(
            () => dataAccess.Insert(
                new ChannelEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Second",
                    Slug = "duplicate-slug"
                }
            )
        );
    }
}
