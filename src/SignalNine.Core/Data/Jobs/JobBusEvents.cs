using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public enum JobTerminalState
{
    Completed = 0,
    Failed = 1,
    Canceled = 2,
    Retry = 3
}

public sealed record JobProgressEvent(
    Guid JobId,
    int Percent,
    string Message,
    DateTimeOffset At
);

public sealed record JobLogEvent(
    Guid JobId,
    JobLogLevelType Level,
    string Message,
    DateTimeOffset At
);

public sealed record JobResultEvent(
    Guid JobId,
    JobTerminalState State,
    string? Error,
    string? ResultJson,
    DateTimeOffset At
);

public sealed record WorkerHeartbeat(
    Guid WorkerId,
    string Name,
    string Version,
    int RunningJobs,
    int MaxConcurrentJobs,
    IReadOnlyList<Guid> CurrentJobIds,
    DateTimeOffset At
);
