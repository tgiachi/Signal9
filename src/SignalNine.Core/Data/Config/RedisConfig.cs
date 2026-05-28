namespace SignalNine.Core.Data.Config;

public class RedisConfig
{
    /// <summary>
    /// Connection URL (e.g. "redis://localhost:6379"). When null or empty, the system falls
    /// back to the in-memory job queue and bus.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>Redis database index (default 0).</summary>
    public int Database { get; set; } = 0;

    /// <summary>Key prefix for all SignalNine streams/channels (default "signal9:").</summary>
    public string KeyPrefix { get; set; } = "signal9:";
}
