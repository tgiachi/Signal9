using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Types;
using SignalNine.Persistence.Services;
using SignalNine.Tests.Support.Persistence;

namespace SignalNine.Tests.Persistence.Services;

public class FreeSqlFactoryTests : IDisposable
{
    private readonly string _rootDirectory;

    public FreeSqlFactoryTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
    }

    [Fact]
    public void Create_SqliteConfig_ReplacesRootDirectoryToken()
    {
        var directoriesConfig = new DirectoriesConfig(_rootDirectory, Enum.GetNames<DirectoryType>());
        var config = new SignalNineConfig
        {
            DatabaseType = DatabaseType.Sqlite,
            DatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine-test.db"
        };
        var factory = new FreeSqlFactory(directoriesConfig);
        var databasePath = Path.Combine(_rootDirectory, "db", "signalnine-test.db");

        var freeSql = factory.Create(config);
        freeSql.CodeFirst.SyncStructure<TestBroadcastItem>();
        freeSql.Insert(new TestBroadcastItem { Id = "episode-1", Title = "Pilot", SortOrder = 1 }).ExecuteAffrows();

        Assert.True(File.Exists(databasePath));

        if (freeSql is IDisposable disposableFreeSql)
        {
            disposableFreeSql.Dispose();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
