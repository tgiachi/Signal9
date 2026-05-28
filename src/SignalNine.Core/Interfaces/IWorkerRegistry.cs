using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Workers;

namespace SignalNine.Core.Interfaces;

public interface IWorkerRegistry
{
    Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkerInfo>> ListAsync(CancellationToken cancellationToken = default);
}
