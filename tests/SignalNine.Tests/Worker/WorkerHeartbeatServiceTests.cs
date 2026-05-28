// tests/SignalNine.Tests/Worker/WorkerHeartbeatServiceTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;
using SignalNine.Worker.Services;

namespace SignalNine.Tests.Worker;

public class WorkerHeartbeatServiceTests
{
    private static (WorkerHeartbeatService service, InMemoryJobBus bus, WorkerIdentity identity, WorkerRuntimeState state) Build(
        TimeSpan? interval = null)
    {
        var bus = new InMemoryJobBus();
        var identity = new WorkerIdentity(Guid.NewGuid(), "test-worker");
        var config = new SignalNineConfig { JobSystem = new JobSystemConfig { MaxConcurrentJobs = 3 } };
        var state = new WorkerRuntimeState(config);
        var tick = interval ?? TimeSpan.FromMilliseconds(50);
        var service = new WorkerHeartbeatService(bus, identity, state, tick);
        return (service, bus, identity, state);
    }

    [Fact]
    public async Task Service_PublishesAtLeastTwoHeartbeats_WithinWindow()
    {
        var (service, bus, identity, _) = Build(TimeSpan.FromMilliseconds(50));
        var collected = new List<SignalNine.Core.Data.Jobs.WorkerHeartbeat>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subscriber = Task.Run(async () =>
        {
            await foreach (var hb in bus.SubscribeHeartbeatAsync(cts.Token))
            {
                collected.Add(hb);
            }
        });

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await service.StopAsync(CancellationToken.None);
        await cts.CancelAsync();

        try { await subscriber; } catch (OperationCanceledException) { }

        Assert.True(collected.Count >= 2, $"Expected at least 2 heartbeats, got {collected.Count}");
        Assert.All(collected, hb =>
        {
            Assert.Equal(identity.Id, hb.WorkerId);
            Assert.Equal(identity.Name, hb.Name);
        });
    }

    [Fact]
    public async Task Service_HeartbeatCarriesCorrectRunningJobCount()
    {
        var (service, bus, identity, state) = Build(TimeSpan.FromMilliseconds(50));
        var jobId = Guid.NewGuid();
        state.MarkStarted(jobId);

        var firstHb = new TaskCompletionSource<SignalNine.Core.Data.Jobs.WorkerHeartbeat>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subscriber = Task.Run(async () =>
        {
            await foreach (var hb in bus.SubscribeHeartbeatAsync(cts.Token))
            {
                firstHb.TrySetResult(hb);
                break;
            }
        });

        await service.StartAsync(CancellationToken.None);

        var hb = await firstHb.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await service.StopAsync(CancellationToken.None);
        await cts.CancelAsync();
        try { await subscriber; } catch (OperationCanceledException) { }

        Assert.Equal(1, hb.RunningJobs);
        Assert.Contains(jobId, hb.CurrentJobIds);
    }

    [Fact]
    public async Task Service_AfterStop_NoFurtherHeartbeatsArriveWithin200ms()
    {
        var (service, bus, _, _) = Build(TimeSpan.FromMilliseconds(50));
        var collected = new List<SignalNine.Core.Data.Jobs.WorkerHeartbeat>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        var countAfterStop = 0;
        var subscriber = Task.Run(async () =>
        {
            await foreach (var hb in bus.SubscribeHeartbeatAsync(cts.Token))
            {
                countAfterStop++;
            }
        });

        await Task.Delay(200);
        await cts.CancelAsync();
        try { await subscriber; } catch (OperationCanceledException) { }

        Assert.Equal(0, countAfterStop);
    }
}
