using SignalNine.Core.Data.Workers;

namespace SignalNine.Web.Data.Workers;

public sealed record WorkerResponse(
    Guid WorkerId,
    string Name,
    string Version,
    int RunningJobs,
    int MaxConcurrentJobs,
    IReadOnlyList<Guid> CurrentJobIds,
    DateTimeOffset LastSeenAt,
    bool Online
)
{
    public static WorkerResponse From(WorkerInfo info)
    {
        return new WorkerResponse(
            info.WorkerId,
            info.Name,
            info.Version,
            info.RunningJobs,
            info.MaxConcurrentJobs,
            info.CurrentJobIds,
            info.LastSeenAt,
            info.Online
        );
    }
}
