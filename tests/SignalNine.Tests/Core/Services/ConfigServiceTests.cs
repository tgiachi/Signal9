using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Services;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Core.Services;

public class ConfigServiceTests : IDisposable
{
    private const int DefaultJwtExpirationMinutes = 60;
    private const int DefaultMaxConcurrentJobs = 2;
    private const int DefaultMaxLogEntriesPerJob = 500;
    private const int TestJwtExpirationMinutes = 15;
    private const int TestMaxConcurrentJobs = 4;
    private const int TestMaxLogEntriesPerJob = 25;

    private readonly string _rootDirectory;

    public ConfigServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task LoadAsync_MissingConfigFile_CreatesDefaultConfigFile()
    {
        var directoriesConfig = CreateDirectoriesConfig();
        var service = new ConfigService(directoriesConfig);

        var config = await service.LoadAsync();

        Assert.Equal(LogLevelType.Information, config.LogLevel);
        Assert.True(config.LogToFile);
        Assert.Equal(DatabaseType.Sqlite, config.DatabaseType);
        Assert.Equal("sqlite://{ROOT_DIRECTORY}/db/signalnine.db", config.DatabaseUrl);
        Assert.Equal("SignalNine", config.Jwt.Issuer);
        Assert.Equal("SignalNine", config.Jwt.Audience);
        Assert.Equal("signalnine-development-secret-change-before-production", config.Jwt.Secret);
        Assert.Equal(DefaultJwtExpirationMinutes, config.Jwt.ExpirationMinutes);
        Assert.Equal(DefaultMaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
        Assert.Equal(DefaultMaxLogEntriesPerJob, config.JobSystem.MaxLogEntriesPerJob);
        Assert.True(File.Exists(service.ConfigPath));
    }

    [Fact]
    public async Task LoadAsync_SavedConfigFile_ReturnsSavedConfig()
    {
        var directoriesConfig = CreateDirectoriesConfig();
        var service = new ConfigService(directoriesConfig);
        var expectedConfig = new SignalNineConfig
        {
            LogLevel = LogLevelType.Debug,
            LogToFile = false,
            DatabaseUrl = "postgres://localhost/signalnine",
            DatabaseType = DatabaseType.PostgreSql,
            Jwt =
            {
                Issuer = "SignalNine.Tests",
                Audience = "SignalNine.Tests.Client",
                Secret = "signalnine-test-secret-with-enough-length",
                ExpirationMinutes = TestJwtExpirationMinutes
            },
            JobSystem =
            {
                MaxConcurrentJobs = TestMaxConcurrentJobs,
                MaxLogEntriesPerJob = TestMaxLogEntriesPerJob
            }
        };

        await service.SaveAsync(expectedConfig);

        var config = await service.LoadAsync();

        Assert.Equal(expectedConfig.LogLevel, config.LogLevel);
        Assert.Equal(expectedConfig.LogToFile, config.LogToFile);
        Assert.Equal(expectedConfig.DatabaseUrl, config.DatabaseUrl);
        Assert.Equal(expectedConfig.DatabaseType, config.DatabaseType);
        Assert.Equal(expectedConfig.Jwt.Issuer, config.Jwt.Issuer);
        Assert.Equal(expectedConfig.Jwt.Audience, config.Jwt.Audience);
        Assert.Equal(expectedConfig.Jwt.Secret, config.Jwt.Secret);
        Assert.Equal(expectedConfig.Jwt.ExpirationMinutes, config.Jwt.ExpirationMinutes);
        Assert.Equal(expectedConfig.JobSystem.MaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
        Assert.Equal(expectedConfig.JobSystem.MaxLogEntriesPerJob, config.JobSystem.MaxLogEntriesPerJob);
    }

    [Fact]
    public async Task LoadAsync_LegacyConfigFile_AddsJwtDefaults()
    {
        var directoriesConfig = CreateDirectoriesConfig();
        var service = new ConfigService(directoriesConfig);

        await File.WriteAllTextAsync(
            service.ConfigPath,
            """
            LogLevel = 3
            LogToFile = true
            DatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db"
            DatabaseType = 0
            """
        );

        var config = await service.LoadAsync();
        var savedToml = await File.ReadAllTextAsync(service.ConfigPath);

        Assert.Equal("SignalNine", config.Jwt.Issuer);
        Assert.Contains("Jwt", savedToml);
        Assert.Contains("ExpirationMinutes", savedToml);
    }

    [Fact]
    public async Task LoadAsync_LegacyConfigFile_AddsJobSystemDefaults()
    {
        var directoriesConfig = CreateDirectoriesConfig();
        var service = new ConfigService(directoriesConfig);

        await File.WriteAllTextAsync(
            service.ConfigPath,
            """
            LogLevel = 3
            LogToFile = true
            DatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db"
            DatabaseType = 0
            [Jwt]
            Issuer = "SignalNine"
            Audience = "SignalNine"
            Secret = "signalnine-development-secret-change-before-production"
            ExpirationMinutes = 60
            """
        );

        var config = await service.LoadAsync();
        var savedToml = await File.ReadAllTextAsync(service.ConfigPath);

        Assert.Equal(DefaultMaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
        Assert.Contains("JobSystem", savedToml);
        Assert.Contains("MaxConcurrentJobs", savedToml);
    }

    private DirectoriesConfig CreateDirectoriesConfig()
        => new(_rootDirectory, Enum.GetNames<DirectoryType>());

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
