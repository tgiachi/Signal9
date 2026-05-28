using System.Text.Json;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Workers;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Workers;
using SignalNine.Persistence.Interfaces;

namespace SignalNine.Web.Services.Workers;

public sealed class SqliteWorkerRegistry : IWorkerRegistry
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDataAccess<WorkerHeartbeatEntity> _store;

    public SqliteWorkerRegistry(IDataAccess<WorkerHeartbeatEntity> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        var existing = _store.GetByKey(heartbeat.WorkerId);
        if (existing is null)
        {
            var entity = new WorkerHeartbeatEntity
            {
                WorkerId = heartbeat.WorkerId,
                Name = heartbeat.Name,
                Version = heartbeat.Version,
                RunningJobs = heartbeat.RunningJobs,
                MaxConcurrentJobs = heartbeat.MaxConcurrentJobs,
                CurrentJobIdsJson = JsonSerializer.Serialize(heartbeat.CurrentJobIds, JsonOpts),
                LastSeenAt = heartbeat.At.UtcDateTime
            };
            _store.Insert(entity);
        }
        else
        {
            existing.Name = heartbeat.Name;
            existing.Version = heartbeat.Version;
            existing.RunningJobs = heartbeat.RunningJobs;
            existing.MaxConcurrentJobs = heartbeat.MaxConcurrentJobs;
            existing.CurrentJobIdsJson = JsonSerializer.Serialize(heartbeat.CurrentJobIds, JsonOpts);
            existing.LastSeenAt = heartbeat.At.UtcDateTime;
            _store.Update(existing);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkerInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var list = _store.List()
            .Select(e => new WorkerInfo(
                e.WorkerId,
                e.Name,
                e.Version,
                e.RunningJobs,
                e.MaxConcurrentJobs,
                DeserializeIds(e.CurrentJobIdsJson),
                new DateTimeOffset(DateTime.SpecifyKind(e.LastSeenAt, DateTimeKind.Utc)),
                now - new DateTimeOffset(DateTime.SpecifyKind(e.LastSeenAt, DateTimeKind.Utc)) < OnlineThreshold
            ))
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkerInfo>>(list);
    }

    private static IReadOnlyList<Guid> DeserializeIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Guid>();
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOpts) ?? new List<Guid>();
        }
        catch
        {
            return Array.Empty<Guid>();
        }
    }
}
