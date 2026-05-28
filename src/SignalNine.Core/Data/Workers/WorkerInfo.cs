namespace SignalNine.Core.Data.Workers;

public sealed record WorkerInfo(
    Guid WorkerId,
    string Name,
    string Version,
    int RunningJobs,
    int MaxConcurrentJobs,
    IReadOnlyList<Guid> CurrentJobIds,
    DateTimeOffset LastSeenAt,
    bool Online
);
