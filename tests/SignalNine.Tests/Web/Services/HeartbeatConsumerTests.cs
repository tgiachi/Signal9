using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Workers;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web.Services;

public sealed class HeartbeatConsumerTests
{
    // -------------------------------------------------------------------------
    // Test 1: Single heartbeat round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SingleHeartbeat_RegistryReceivesIt()
    {
        var bus = new HbSignallingJobBus();
        var registry = new HbStubWorkerRegistry();
        var scopeFactory = new HbScopeFactory(registry);
        using var cts = new CancellationTokenSource();

        var consumer = new HeartbeatConsumer(bus, scopeFactory);
        await consumer.StartAsync(cts.Token);
        await bus.WaitForSubscriberAsync(); // consumer is now in await foreach

        var heartbeat = MakeHeartbeat();
        await bus.PublishHeartbeatAsync(heartbeat, CancellationToken.None);

        await WaitForCount(registry, 1);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        Assert.Single(registry.Received);
        Assert.Equal(heartbeat.WorkerId, registry.Received[0].WorkerId);
    }

    // -------------------------------------------------------------------------
    // Test 2: Multiple heartbeats — all recorded in order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_MultipleHeartbeats_AllRecordedInOrder()
    {
        var bus = new HbSignallingJobBus();
        var registry = new HbStubWorkerRegistry();
        var scopeFactory = new HbScopeFactory(registry);
        using var cts = new CancellationTokenSource();

        var consumer = new HeartbeatConsumer(bus, scopeFactory);
        await consumer.StartAsync(cts.Token);
        await bus.WaitForSubscriberAsync();

        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
        {
            await bus.PublishHeartbeatAsync(MakeHeartbeat(id), CancellationToken.None);
        }

        await WaitForCount(registry, 3);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(3, registry.Received.Count);
        for (var i = 0; i < ids.Length; i++)
        {
            Assert.Equal(ids[i], registry.Received[i].WorkerId);
        }
    }

    // -------------------------------------------------------------------------
    // Test 3: Cancellation exits cleanly — no exception bubbles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_Cancellation_ExitsCleanly()
    {
        var bus = new HbSignallingJobBus();
        var registry = new HbStubWorkerRegistry();
        var scopeFactory = new HbScopeFactory(registry);
        using var cts = new CancellationTokenSource();

        var consumer = new HeartbeatConsumer(bus, scopeFactory);
        await consumer.StartAsync(cts.Token);
        await bus.WaitForSubscriberAsync();

        await bus.PublishHeartbeatAsync(MakeHeartbeat(), CancellationToken.None);
        await WaitForCount(registry, 1);

        await cts.CancelAsync();

        var ex = await Record.ExceptionAsync(() => consumer.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // Test 4: Upsert failure logged, loop continues with next heartbeat
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UpsertThrowsOnFirstCall_LoopContinues()
    {
        var bus = new HbSignallingJobBus();
        var registry = new HbThrowOnFirstStubWorkerRegistry();
        var scopeFactory = new HbScopeFactory(registry);
        using var cts = new CancellationTokenSource();

        var consumer = new HeartbeatConsumer(bus, scopeFactory);
        await consumer.StartAsync(cts.Token);
        await bus.WaitForSubscriberAsync();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await bus.PublishHeartbeatAsync(MakeHeartbeat(id1), CancellationToken.None);
        await bus.PublishHeartbeatAsync(MakeHeartbeat(id2), CancellationToken.None);

        await WaitForCount(registry, 1);

        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        // First call threw; second was still recorded.
        Assert.Single(registry.Received);
        Assert.Equal(id2, registry.Received[0].WorkerId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WorkerHeartbeat MakeHeartbeat(Guid? workerId = null)
    {
        return new WorkerHeartbeat(
            workerId ?? Guid.NewGuid(),
            "test-worker",
            "1.0.0.0",
            0,
            4,
            Array.Empty<Guid>(),
            DateTimeOffset.UtcNow
        );
    }

    private static async Task WaitForCount(HbStubWorkerRegistryBase registry, int expected, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (registry.Received.Count < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}

// -------------------------------------------------------------------------
// Stubs
// -------------------------------------------------------------------------

/// <summary>
/// Wraps InMemoryJobBus and exposes a semaphore that is signalled the moment
/// a subscriber calls SubscribeHeartbeatAsync, so tests can await the
/// consumer being fully ready before publishing.
/// </summary>
internal sealed class HbSignallingJobBus : IJobBus
{
    private readonly InMemoryJobBus _inner = new();
    private readonly SemaphoreSlim _subscribed = new(0, 1);

    public Task PublishProgressAsync(JobProgressEvent e, CancellationToken ct = default)
        => _inner.PublishProgressAsync(e, ct);

    public Task PublishLogAsync(JobLogEvent e, CancellationToken ct = default)
        => _inner.PublishLogAsync(e, ct);

    public Task PublishResultAsync(JobResultEvent e, CancellationToken ct = default)
        => _inner.PublishResultAsync(e, ct);

    public Task PublishCancelAsync(Guid jobId, CancellationToken ct = default)
        => _inner.PublishCancelAsync(jobId, ct);

    public Task PublishHeartbeatAsync(WorkerHeartbeat h, CancellationToken ct = default)
        => _inner.PublishHeartbeatAsync(h, ct);

    public IAsyncEnumerable<JobProgressEvent> SubscribeProgressAsync(CancellationToken ct)
        => _inner.SubscribeProgressAsync(ct);

    public IAsyncEnumerable<JobLogEvent> SubscribeLogAsync(CancellationToken ct)
        => _inner.SubscribeLogAsync(ct);

    public IAsyncEnumerable<JobResultEvent> SubscribeResultAsync(CancellationToken ct)
        => _inner.SubscribeResultAsync(ct);

    public IAsyncEnumerable<Guid> SubscribeCancelAsync(CancellationToken ct)
        => _inner.SubscribeCancelAsync(ct);

    public async IAsyncEnumerable<WorkerHeartbeat> SubscribeHeartbeatAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        _subscribed.Release();
        await foreach (var h in _inner.SubscribeHeartbeatAsync(ct).ConfigureAwait(false))
        {
            yield return h;
        }
    }

    public Task WaitForSubscriberAsync(int timeoutMs = 3000)
        => _subscribed.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
}

internal abstract class HbStubWorkerRegistryBase : IWorkerRegistry
{
    private readonly ConcurrentQueue<WorkerHeartbeat> _received = new();

    public IReadOnlyList<WorkerHeartbeat> Received => _received.ToList();

    public abstract Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default);

    protected void Record(WorkerHeartbeat heartbeat)
    {
        _received.Enqueue(heartbeat);
    }

    public Task<IReadOnlyList<WorkerInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkerInfo> empty = Array.Empty<WorkerInfo>();
        return Task.FromResult(empty);
    }
}

internal sealed class HbStubWorkerRegistry : HbStubWorkerRegistryBase
{
    public override Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        Record(heartbeat);
        return Task.CompletedTask;
    }
}

internal sealed class HbThrowOnFirstStubWorkerRegistry : HbStubWorkerRegistryBase
{
    private int _callCount;

    public override Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _callCount);
        if (count == 1)
        {
            throw new InvalidOperationException("Simulated upsert failure.");
        }

        Record(heartbeat);
        return Task.CompletedTask;
    }
}

internal sealed class HbScopeFactory : IServiceScopeFactory
{
    private readonly IWorkerRegistry _registry;

    public HbScopeFactory(IWorkerRegistry registry)
    {
        _registry = registry;
    }

    public IServiceScope CreateScope()
    {
        return new HbServiceScope(_registry);
    }
}

internal sealed class HbServiceScope : IServiceScope
{
    public HbServiceScope(IWorkerRegistry registry)
    {
        ServiceProvider = new HbServiceProvider(registry);
    }

    public IServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
    }
}

internal sealed class HbServiceProvider : IServiceProvider
{
    private readonly IWorkerRegistry _registry;

    public HbServiceProvider(IWorkerRegistry registry)
    {
        _registry = registry;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IWorkerRegistry)) return _registry;
        return null;
    }
}
