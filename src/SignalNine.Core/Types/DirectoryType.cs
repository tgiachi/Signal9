namespace SignalNine.Core.Types;

/// <summary>
/// Defines the directories used by SignalNine.
/// </summary>
public enum DirectoryType
{
    /// <summary>Directory for storing application configuration files.</summary>
    Configs,

    /// <summary>Directory for storing log files.</summary>
    Logs,

    /// <summary>Directory for storing database files.</summary>
    Db,

    /// <summary>Directory for storing channel bumpers.</summary>
    Bumpers,

    /// <summary>Directory for storing commercial media.</summary>
    Commercials,

    /// <summary>Directory for storing television show media.</summary>
    TvShows,

    /// <summary>Directory for storing movie media.</summary>
    Movies,

    /// <summary>Directory for uploaded assets (logos, future thumbnails, …).</summary>
    Assets
}
