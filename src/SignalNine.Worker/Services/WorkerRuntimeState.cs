using System.Collections.Concurrent;
using SignalNine.Core.Data.Config;

namespace SignalNine.Worker.Services;

public sealed class WorkerRuntimeState
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();
    private readonly int _capacity;

    public WorkerRuntimeState(SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _capacity = config.JobSystem.MaxConcurrentJobs;
    }

    public int Capacity => _capacity;

    public int RunningCount => _running.Count;

    public void MarkStarted(Guid jobId)
    {
        _running.TryAdd(jobId, 0);
    }

    public void MarkFinished(Guid jobId)
    {
        _running.TryRemove(jobId, out _);
    }

    public IReadOnlyList<Guid> Snapshot()
    {
        return _running.Keys.ToList();
    }
}
