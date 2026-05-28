using Microsoft.AspNetCore.Identity;
using Serilog;
using SignalNine.Persistence.Entities.Users;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services;

internal sealed class DefaultUserSeeder
{
    private const string DefaultUsername = "admin";
    private const string DefaultEmail = "admin@signalnine.local";
    private const string DefaultPassword = "admin";

    private readonly ILogger _logger = Log.ForContext<DefaultUserSeeder>();
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<UserEntity> _dataAccess;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;

    public DefaultUserSeeder(
        IFreeSql freeSql,
        IDataAccess<UserEntity> dataAccess,
        IPasswordHasher<UserEntity> passwordHasher
    )
    {
        ArgumentNullException.ThrowIfNull(freeSql);
        ArgumentNullException.ThrowIfNull(dataAccess);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        _freeSql = freeSql;
        _dataAccess = dataAccess;
        _passwordHasher = passwordHasher;
    }

    public void Seed()
    {
        _freeSql.CodeFirst.SyncStructure<UserEntity>();

        var existingUser = _dataAccess.List()
                                      .FirstOrDefault(
                                          user => string.Equals(
                                              user.Username,
                                              DefaultUsername,
                                              StringComparison.OrdinalIgnoreCase
                                          )
                                      );

        if (existingUser is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Username = DefaultUsername,
            Email = DefaultEmail,
            Role = UserRoleType.Admin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPassword);

        _dataAccess.Insert(user);
        _logger.Information("Seeded default administrator user {Username}", DefaultUsername);
    }
}
