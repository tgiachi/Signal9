namespace SignalNine.Core.Types;

/// <summary>
/// Defines the directories used by SignalNine.
/// </summary>
public enum DirectoryType
{
    /// <summary>Application configuration files.</summary>
    Configs,

    /// <summary>Log files.</summary>
    Logs,

    /// <summary>Database files.</summary>
    Db,

    /// <summary>Uploaded assets (logos, future thumbnails, …).</summary>
    Assets
}
