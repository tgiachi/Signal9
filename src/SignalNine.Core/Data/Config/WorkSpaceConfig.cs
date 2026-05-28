namespace SignalNine.Core.Data.Config;

public class WorkSpaceConfig
{
    /// <summary>Root path mounted on both web and workers (NFS shared).</summary>
    public string Path { get; set; } = "/signal9_work";

    /// <summary>Remove the work dir after successful result processing.</summary>
    public bool CleanupAfterProcessing { get; set; } = true;

    /// <summary>Janitor removes work dirs older than this many hours when their job is no longer in flight.</summary>
    public int OrphanCleanupHours { get; set; } = 24;
}
