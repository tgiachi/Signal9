using FreeSql;
using SignalNine.Persistence.Entities.Users;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;
using SignalNine.Persistence.Types;

namespace SignalNine.Tests.Persistence.Data.Users;

public class UserEntityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;

    public UserEntityTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<UserEntity>();
    }

    [Fact]
    public void Insert_GetByKey_ReturnsPersistedUser()
    {
        IDataAccess<UserEntity> dataAccess = new FreeSqlDataAccess<UserEntity>(_freeSql);
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var updatedAt = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@signalnine.local",
            PasswordHash = "hash",
            Role = UserRoleType.Admin,
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        dataAccess.Insert(user);

        var persistedUser = dataAccess.GetByKey(user.Id);

        Assert.NotNull(persistedUser);
        Assert.Equal(user.Id, persistedUser.Id);
        Assert.Equal("admin", persistedUser.Username);
        Assert.Equal("admin@signalnine.local", persistedUser.Email);
        Assert.Equal("hash", persistedUser.PasswordHash);
        Assert.Equal(UserRoleType.Admin, persistedUser.Role);
        Assert.True(persistedUser.IsActive);
        Assert.Equal(createdAt, persistedUser.CreatedAt.ToUniversalTime());
        Assert.Equal(updatedAt, persistedUser.UpdatedAt.ToUniversalTime());
        Assert.Null(persistedUser.LastLoginAt);
    }

    [Fact]
    public void Insert_NewUser_UsesJwtReadyDefaults()
    {
        var user = new UserEntity();

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(UserRoleType.User, user.Role);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void Insert_DuplicateUsername_RejectsDuplicateUsername()
    {
        IDataAccess<UserEntity> dataAccess = new FreeSqlDataAccess<UserEntity>(_freeSql);

        dataAccess.Insert(
            new UserEntity
            {
                Username = "admin",
                Email = "admin@signalnine.local",
                PasswordHash = "hash"
            }
        );

        Assert.ThrowsAny<Exception>(
            () => dataAccess.Insert(
                new UserEntity
                {
                    Username = "admin",
                    Email = "other@signalnine.local",
                    PasswordHash = "hash"
                }
            )
        );
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
