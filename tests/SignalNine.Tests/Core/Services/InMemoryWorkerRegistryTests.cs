using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class InMemoryWorkerRegistryTests
{
    private static WorkerHeartbeat MakeBeat(Guid id, DateTimeOffset at)
        => new(id, "w1", "1.0", 0, 2, Array.Empty<Guid>(), at);

    [Fact]
    public async Task Upsert_then_List_returns_worker()
    {
        var reg = new InMemoryWorkerRegistry();
        var id = Guid.NewGuid();
        await reg.UpsertHeartbeatAsync(MakeBeat(id, DateTimeOffset.UtcNow));
        var list = await reg.ListAsync();
        Assert.Single(list);
        Assert.Equal(id, list[0].WorkerId);
        Assert.True(list[0].Online);
    }

    [Fact]
    public async Task Upsert_is_idempotent_per_workerId()
    {
        var reg = new InMemoryWorkerRegistry();
        var id = Guid.NewGuid();
        await reg.UpsertHeartbeatAsync(MakeBeat(id, DateTimeOffset.UtcNow.AddSeconds(-10)));
        await reg.UpsertHeartbeatAsync(MakeBeat(id, DateTimeOffset.UtcNow));
        var list = await reg.ListAsync();
        Assert.Single(list);
    }

    [Fact]
    public async Task Online_flips_to_false_after_30s()
    {
        var reg = new InMemoryWorkerRegistry();
        var id = Guid.NewGuid();
        await reg.UpsertHeartbeatAsync(MakeBeat(id, DateTimeOffset.UtcNow.AddSeconds(-31)));
        var list = await reg.ListAsync();
        Assert.False(list[0].Online);
    }
}
