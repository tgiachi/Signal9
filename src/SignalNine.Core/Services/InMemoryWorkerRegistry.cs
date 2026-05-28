using System.Collections.Concurrent;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Workers;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public sealed class InMemoryWorkerRegistry : IWorkerRegistry
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, WorkerHeartbeat> _beats = new();

    public Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        _beats[heartbeat.WorkerId] = heartbeat;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkerInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var list = _beats.Values
            .Select(h => new WorkerInfo(
                h.WorkerId, h.Name, h.Version,
                h.RunningJobs, h.MaxConcurrentJobs, h.CurrentJobIds,
                h.At, Online: now - h.At < OnlineWindow))
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkerInfo>>(list);
    }
}
