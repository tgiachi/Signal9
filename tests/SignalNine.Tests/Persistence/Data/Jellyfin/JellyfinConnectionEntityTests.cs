using FreeSql;
using SignalNine.Persistence.Entities.Jellyfin;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;

namespace SignalNine.Tests.Persistence.Data.Jellyfin;

public class JellyfinConnectionEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<JellyfinConnectionEntity> _dataAccess;

    public JellyfinConnectionEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<JellyfinConnectionEntity>();
        _dataAccess = new FreeSqlDataAccess<JellyfinConnectionEntity>(_freeSql);
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Roundtrip_PersistsAllFields()
    {
        var verifiedAt = DateTime.UtcNow.AddMinutes(-1);
        var entity = new JellyfinConnectionEntity
        {
            Id = Guid.NewGuid(),
            BaseUrl = "https://jellyfin.example.com",
            EncryptedApiKey = "enc:fake-cipher-bytes",
            IsActive = true,
            LastVerifiedAt = verifiedAt
        };

        _dataAccess.Insert(entity);
        var fetched = _dataAccess.GetByKey(entity.Id);

        Assert.NotNull(fetched);
        Assert.Equal("https://jellyfin.example.com", fetched!.BaseUrl);
        Assert.Equal("enc:fake-cipher-bytes", fetched.EncryptedApiKey);
        Assert.True(fetched.IsActive);
        Assert.NotNull(fetched.LastVerifiedAt);
    }

    [Fact]
    public void EncryptedApiKey_Persists_LargeCipherText()
    {
        var bigCipher = new string('X', 1500);
        var entity = new JellyfinConnectionEntity
        {
            Id = Guid.NewGuid(),
            BaseUrl = "https://x.local",
            EncryptedApiKey = bigCipher
        };

        _dataAccess.Insert(entity);
        var fetched = _dataAccess.GetByKey(entity.Id);

        Assert.NotNull(fetched);
        Assert.Equal(bigCipher, fetched!.EncryptedApiKey);
    }
}
