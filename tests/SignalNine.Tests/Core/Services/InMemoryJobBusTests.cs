// tests/SignalNine.Tests/Core/Services/InMemoryJobBusTests.cs
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class InMemoryJobBusTests
{
    [Fact]
    public async Task Publish_then_Subscribe_receives_event()
    {
        var bus = new InMemoryJobBus();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeProgressAsync(cts.Token))
            {
                return e;
            }
            return null;
        });

        await Task.Delay(50); // let subscriber wire up
        await bus.PublishProgressAsync(new JobProgressEvent(Guid.NewGuid(), 50, "halfway", DateTimeOffset.UtcNow));

        var received = await subscribeTask;
        Assert.NotNull(received);
        Assert.Equal(50, received!.Percent);
    }

    [Fact]
    public async Task FanOut_two_subscribers_both_receive()
    {
        var bus = new InMemoryJobBus();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var t1 = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeResultAsync(cts.Token)) return e;
            return null;
        });
        var t2 = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeResultAsync(cts.Token)) return e;
            return null;
        });

        await Task.Delay(50);
        await bus.PublishResultAsync(new JobResultEvent(Guid.NewGuid(), JobTerminalState.Completed, null, null, DateTimeOffset.UtcNow));

        var r1 = await t1;
        var r2 = await t2;
        Assert.NotNull(r1);
        Assert.NotNull(r2);
    }

    [Fact]
    public async Task Cancel_channel_works()
    {
        var bus = new InMemoryJobBus();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var targetId = Guid.NewGuid();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var id in bus.SubscribeCancelAsync(cts.Token)) return id;
            return Guid.Empty;
        });

        await Task.Delay(50);
        await bus.PublishCancelAsync(targetId);

        var received = await subscribeTask;
        Assert.Equal(targetId, received);
    }
}
