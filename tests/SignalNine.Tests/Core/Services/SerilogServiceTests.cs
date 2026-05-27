using Serilog;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Logging;
using SignalNine.Core.Services;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Core.Services;

public class SerilogServiceTests : IDisposable
{
    private readonly string _rootDirectory;

    public SerilogServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        EventSink.ClearSubscribers();
        Log.CloseAndFlush();
    }

    [Fact]
    public void Configure_LogLevelFromConfig_FiltersBelowConfiguredLevel()
    {
        var service = new SerilogService(CreateDirectoriesConfig());
        var receivedMessages = new List<string>();
        EventSink.OnLogReceived += (_, eventData) => receivedMessages.Add(eventData.Message);

        service.Configure(
            new SignalNineConfig
            {
                LogLevel = LogLevelType.Warning,
                LogToFile = false
            }
        );

        Log.Information("Hidden information message");
        Log.Warning("Visible warning message");
        Log.CloseAndFlush();

        Assert.DoesNotContain("Hidden information message", receivedMessages);
        Assert.Contains("Visible warning message", receivedMessages);
    }

    [Fact]
    public void Configure_LogToFileEnabled_WritesLogsToLogsDirectory()
    {
        var directoriesConfig = CreateDirectoriesConfig();
        var service = new SerilogService(directoriesConfig);

        service.Configure(
            new SignalNineConfig
            {
                LogLevel = LogLevelType.Information,
                LogToFile = true
            }
        );

        Log.Information("File sink message");
        Log.CloseAndFlush();

        var logFiles = Directory.GetFiles(directoriesConfig.GetPath(DirectoryType.Logs), "signalnine-*.log");

        Assert.Single(logFiles);
        Assert.Contains("File sink message", File.ReadAllText(logFiles[0]));
    }

    private DirectoriesConfig CreateDirectoriesConfig()
        => new(_rootDirectory, Enum.GetNames<DirectoryType>());

    public void Dispose()
    {
        EventSink.ClearSubscribers();
        Log.CloseAndFlush();

        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
