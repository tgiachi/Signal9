using FreeSql.DataAnnotations;

namespace SignalNine.Persistence.Entities.Workers;

[Table(Name = "worker_heartbeats")]
public class WorkerHeartbeatEntity
{
    [Column(IsPrimary = true)]
    public Guid WorkerId { get; set; }

    [Column(IsNullable = false, StringLength = 200)]
    public string Name { get; set; } = string.Empty;

    [Column(IsNullable = false, StringLength = 50)]
    public string Version { get; set; } = string.Empty;

    [Column(IsNullable = false)]
    public int RunningJobs { get; set; }

    [Column(IsNullable = false)]
    public int MaxConcurrentJobs { get; set; }

    /// <summary>JSON-encoded array of GUIDs.</summary>
    [Column(IsNullable = false, StringLength = -1)]
    public string CurrentJobIdsJson { get; set; } = "[]";

    [Column(IsNullable = false)]
    public DateTime LastSeenAt { get; set; }
}
