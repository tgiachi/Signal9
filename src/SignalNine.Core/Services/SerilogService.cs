using Serilog;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Extensions.Logger;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Logging;
using SignalNine.Core.Types;

namespace SignalNine.Core.Services;

public class SerilogService : ISerilogService
{
    private const string LogFileName = "signalnine-.log";

    private readonly DirectoriesConfig _directoriesConfig;

    public SerilogService(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);

        _directoriesConfig = directoriesConfig;
    }

    public void Configure(SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Log.CloseAndFlush();

        if (config.LogLevel == LogLevelType.None)
        {
            Log.Logger = new LoggerConfiguration().CreateLogger();

            return;
        }

        var loggerConfiguration = new LoggerConfiguration()
                                  .MinimumLevel.Is(config.LogLevel.ToSerilogLogLevel())
                                  .Enrich.FromLogContext()
                                  .WriteTo.Console()
                                  .WriteTo.EventSink();

        if (config.LogToFile)
        {
            var logFilePath = Path.Combine(_directoriesConfig.GetPath(DirectoryType.Logs), LogFileName);
            loggerConfiguration.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day);
        }

        Log.Logger = loggerConfiguration.CreateLogger();
    }
}
