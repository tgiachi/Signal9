using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Config;

public class SignalNineConfig
{
    public LogLevelType LogLevel { get; set; } = LogLevelType.Information;

    public bool LogToFile { get; set; } = true;

    public string DatabaseUrl { get; set; } = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db";

    public DatabaseType DatabaseType { get; set; } = DatabaseType.Sqlite;

    public JwtConfig Jwt { get; set; } = new();

    public JobSystemConfig JobSystem { get; set; } = new();

    public FfmpegPoolConfig FfmpegPool { get; set; } = new();
}
