using SignalNine.Core.Types;

namespace SignalNine.Web.Data.Jobs;

/// <summary>
/// Represents the current status of a job.
/// </summary>
public sealed record JobResponse(
    Guid Id,
    string Type,
    JobStateType State,
    int ProgressPercent,
    string ProgressMessage,
    string Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt
);
