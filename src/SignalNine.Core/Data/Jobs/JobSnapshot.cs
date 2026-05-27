using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobSnapshot
{
    public Guid Id { get; set; }

    public string Type { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";

    public JobStateType State { get; set; }

    public JobProgressSnapshot Progress { get; set; } = new();

    public string Error { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
