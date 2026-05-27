using SignalNine.Core.Types;

namespace SignalNine.Web.Data.Jobs;

/// <summary>
/// Represents one retained log entry for a job.
/// </summary>
public sealed record JobLogResponse(
    long Sequence,
    Guid JobId,
    DateTimeOffset Timestamp,
    JobLogLevelType Level,
    string Message
);
