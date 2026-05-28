using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Workers;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Services.Workers;

namespace SignalNine.Tests.Web.Services.Workers;

public sealed class SqliteWorkerRegistryTests
{
    private static WorkerHeartbeat MakeBeat(
        Guid workerId,
        string name = "worker-1",
        string version = "1.0.0",
        int runningJobs = 0,
        int maxConcurrentJobs = 4,
        IReadOnlyList<Guid>? currentJobIds = null,
        DateTimeOffset? at = null)
    {
        return new WorkerHeartbeat(
            workerId,
            name,
            version,
            runningJobs,
            maxConcurrentJobs,
            currentJobIds ?? Array.Empty<Guid>(),
            at ?? DateTimeOffset.UtcNow
        );
    }

    // -------------------------------------------------------------------------
    // Test 1: First heartbeat → INSERT
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHeartbeat_FirstHeartbeat_InsertsWorkerAndIsOnline()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();

        await registry.UpsertHeartbeatAsync(MakeBeat(workerId, name: "srv-1", version: "2.0.0", runningJobs: 1, maxConcurrentJobs: 8));

        var list = await registry.ListAsync();

        Assert.Single(list);
        Assert.Equal(workerId, list[0].WorkerId);
        Assert.Equal("srv-1", list[0].Name);
        Assert.Equal("2.0.0", list[0].Version);
        Assert.Equal(1, list[0].RunningJobs);
        Assert.Equal(8, list[0].MaxConcurrentJobs);
        Assert.True(list[0].Online);
    }

    // -------------------------------------------------------------------------
    // Test 2: Second heartbeat for same WorkerId → UPDATE (not INSERT)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHeartbeat_SameWorkerId_UpdatesNotInserts()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();

        await registry.UpsertHeartbeatAsync(MakeBeat(workerId, runningJobs: 0));
        await registry.UpsertHeartbeatAsync(MakeBeat(workerId, runningJobs: 2, currentJobIds: new[] { g1, g2 }));

        var list = await registry.ListAsync();

        Assert.Single(list);
        Assert.Equal(2, list[0].RunningJobs);
        Assert.Equal(2, list[0].CurrentJobIds.Count);
        Assert.Contains(g1, list[0].CurrentJobIds);
        Assert.Contains(g2, list[0].CurrentJobIds);
    }

    // -------------------------------------------------------------------------
    // Test 3: Two distinct workers → ListAsync returns 2 rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHeartbeat_TwoDistinctWorkers_ReturnsBothInList()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId1 = Guid.NewGuid();
        var workerId2 = Guid.NewGuid();

        await registry.UpsertHeartbeatAsync(MakeBeat(workerId1, name: "worker-a"));
        await registry.UpsertHeartbeatAsync(MakeBeat(workerId2, name: "worker-b"));

        var list = await registry.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, w => w.WorkerId == workerId1 && w.Name == "worker-a");
        Assert.Contains(list, w => w.WorkerId == workerId2 && w.Name == "worker-b");
    }

    // -------------------------------------------------------------------------
    // Test 4: Online flag honors 30s threshold
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListAsync_OldHeartbeat_WorkerIsOffline()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();

        var staleTime = DateTime.UtcNow.AddSeconds(-35);
        store.InsertDirect(new WorkerHeartbeatEntity
        {
            WorkerId = workerId,
            Name = "stale-worker",
            Version = "1.0.0",
            RunningJobs = 0,
            MaxConcurrentJobs = 4,
            CurrentJobIdsJson = "[]",
            LastSeenAt = staleTime
        });

        var list = await registry.ListAsync();

        Assert.Single(list);
        Assert.Equal(workerId, list[0].WorkerId);
        Assert.False(list[0].Online);
    }

    // -------------------------------------------------------------------------
    // Test 5: CurrentJobIds round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHeartbeat_CurrentJobIds_RoundTripsCorrectly()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var g3 = Guid.NewGuid();

        await registry.UpsertHeartbeatAsync(MakeBeat(workerId, currentJobIds: new[] { g1, g2, g3 }));

        var list = await registry.ListAsync();

        Assert.Single(list);
        Assert.Equal(3, list[0].CurrentJobIds.Count);
        Assert.Contains(g1, list[0].CurrentJobIds);
        Assert.Contains(g2, list[0].CurrentJobIds);
        Assert.Contains(g3, list[0].CurrentJobIds);
    }

    // -------------------------------------------------------------------------
    // Test 6: Empty CurrentJobIds → empty list (not null, no exception)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHeartbeat_EmptyCurrentJobIds_ReturnsEmptyList()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();

        await registry.UpsertHeartbeatAsync(MakeBeat(workerId, currentJobIds: Array.Empty<Guid>()));

        var list = await registry.ListAsync();

        Assert.Single(list);
        Assert.NotNull(list[0].CurrentJobIds);
        Assert.Empty(list[0].CurrentJobIds);
    }

    // -------------------------------------------------------------------------
    // Test 7: Malformed JSON in DB → CurrentJobIds = [] (no throw)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListAsync_MalformedCurrentJobIdsJson_ReturnsEmptyListWithoutThrowing()
    {
        var store = new StubWorkerDataAccess();
        IWorkerRegistry registry = new SqliteWorkerRegistry(store);
        var workerId = Guid.NewGuid();

        store.InsertDirect(new WorkerHeartbeatEntity
        {
            WorkerId = workerId,
            Name = "broken-worker",
            Version = "1.0.0",
            RunningJobs = 0,
            MaxConcurrentJobs = 4,
            CurrentJobIdsJson = "not-json",
            LastSeenAt = DateTime.UtcNow
        });

        var ex = await Record.ExceptionAsync(() => registry.ListAsync());

        Assert.Null(ex);
        var list = await registry.ListAsync();
        Assert.Single(list);
        Assert.NotNull(list[0].CurrentJobIds);
        Assert.Empty(list[0].CurrentJobIds);
    }
}

// -------------------------------------------------------------------------
// Stubs — internal to this file
// -------------------------------------------------------------------------

internal sealed class StubWorkerDataAccess : IDataAccess<WorkerHeartbeatEntity>
{
    private readonly Dictionary<Guid, WorkerHeartbeatEntity> _store = new();

    /// <summary>Allows tests to pre-populate the store without going through UpsertHeartbeatAsync.</summary>
    public void InsertDirect(WorkerHeartbeatEntity entity)
    {
        _store[entity.WorkerId] = entity;
    }

    public WorkerHeartbeatEntity? GetByKey(object key)
    {
        var id = (Guid)key;
        return _store.TryGetValue(id, out var entity) ? entity : null;
    }

    public IReadOnlyList<WorkerHeartbeatEntity> List()
    {
        return _store.Values.ToList();
    }

    public WorkerHeartbeatEntity Insert(WorkerHeartbeatEntity entity)
    {
        _store[entity.WorkerId] = entity;
        return entity;
    }

    public int Update(WorkerHeartbeatEntity entity)
    {
        _store[entity.WorkerId] = entity;
        return 1;
    }

    public int Delete(object key)
    {
        var id = (Guid)key;
        return _store.Remove(id) ? 1 : 0;
    }
}
