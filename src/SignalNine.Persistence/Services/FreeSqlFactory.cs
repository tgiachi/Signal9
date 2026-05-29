using FreeSql;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Extensions.Directories;
using SignalNine.Core.Types;

namespace SignalNine.Persistence.Services;

public class FreeSqlFactory
{
    private const string SqliteScheme = "sqlite://";
    private const string PostgreSqlScheme = "postgresql://";
    private const string PostgreSqlShortScheme = "postgres://";
    private const string RootDirectoryToken = "{ROOT_DIRECTORY}";

    private readonly DirectoriesConfig _directoriesConfig;

    public FreeSqlFactory(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);

        _directoriesConfig = directoriesConfig;
    }

    public IFreeSql Create(SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new FreeSqlBuilder()
               .UseConnectionString(GetDataType(config.DatabaseType), GetConnectionString(config))
               .UseAutoSyncStructure(true)
               .Build();
    }

    private static DataType GetDataType(DatabaseType databaseType)
        => databaseType switch
        {
            DatabaseType.Sqlite     => DataType.Sqlite,
            DatabaseType.PostgreSql => DataType.PostgreSQL,
            _                       => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null)
        };

    private string GetConnectionString(SignalNineConfig config)
        => config.DatabaseType switch
        {
            DatabaseType.Sqlite     => GetSqliteConnectionString(config.DatabaseUrl),
            DatabaseType.PostgreSql => GetPostgreSqlConnectionString(config.DatabaseUrl),
            _                       => throw new ArgumentOutOfRangeException(nameof(config), config.DatabaseType, null)
        };

    private string GetSqliteConnectionString(string databaseUrl)
    {
        var resolvedDatabaseUrl = ReplaceRootDirectoryToken(databaseUrl);

        if (resolvedDatabaseUrl.StartsWith(SqliteScheme, StringComparison.OrdinalIgnoreCase))
        {
            resolvedDatabaseUrl = resolvedDatabaseUrl[SqliteScheme.Length..];
        }

        if (resolvedDatabaseUrl.Contains('=', StringComparison.Ordinal) ||
            resolvedDatabaseUrl.Contains(';', StringComparison.Ordinal))
        {
            return resolvedDatabaseUrl;
        }

        if (string.Equals(resolvedDatabaseUrl, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return "Data Source=:memory:";
        }

        var databasePath = Path.GetFullPath(resolvedDatabaseUrl.ResolvePathAndEnvs());
        var databaseDirectory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(databaseDirectory) && !Directory.Exists(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        // WAL + Busy Timeout + Pooling lets multiple pipeline workers update concurrently
        // without "database is locked" failures under high write load.
        return $"Data Source={databasePath};Journal Mode=WAL;Synchronous=Normal;Busy Timeout=10000;Pooling=True";
    }

    private string GetPostgreSqlConnectionString(string databaseUrl)
    {
        var resolvedDatabaseUrl = ReplaceRootDirectoryToken(databaseUrl);

        if (resolvedDatabaseUrl.StartsWith(PostgreSqlScheme, StringComparison.OrdinalIgnoreCase) ||
            resolvedDatabaseUrl.StartsWith(PostgreSqlShortScheme, StringComparison.OrdinalIgnoreCase))
        {
            return ConvertPostgreSqlUrl(resolvedDatabaseUrl);
        }

        return resolvedDatabaseUrl;
    }

    private string ReplaceRootDirectoryToken(string databaseUrl)
        => databaseUrl.Replace(RootDirectoryToken, _directoriesConfig.Root, StringComparison.Ordinal);

    private static string ConvertPostgreSqlUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        return $"Host={uri.Host};Port={port};Username={username};Password={password};Database={database}";
    }
}
