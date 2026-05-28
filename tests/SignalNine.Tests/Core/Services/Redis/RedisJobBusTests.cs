// tests/SignalNine.Tests/Core/Services/Redis/RedisJobBusTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services.Redis;
using SignalNine.Tests.Support;

namespace SignalNine.Tests.Core.Services.Redis;

[Collection(RedisCollection.Name)]
public class RedisJobBusTests : IAsyncLifetime
{
    private readonly RedisContainerFixture _redis;
    private readonly string _prefix;
    private RedisJobBus _bus = default!;

    public RedisJobBusTests(RedisContainerFixture redis)
    {
        _redis = redis;
        _prefix = $"test:{Guid.NewGuid():N}:";
    }

    public Task InitializeAsync()
    {
        var cfg = new RedisConfig { Url = _redis.ConnectionString, KeyPrefix = _prefix };
        _bus = new RedisJobBus(_redis.Connection, new RedisStreamKeys(cfg));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Publish_progress_then_Subscribe_receives_event()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = new TaskCompletionSource<JobProgressEvent>();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in _bus.SubscribeProgressAsync(cts.Token))
            {
                received.TrySetResult(e);
                break;
            }
        });

        await Task.Delay(100); // let subscribe wire up
        var sent = new JobProgressEvent(Guid.NewGuid(), 42, "halfway", DateTimeOffset.UtcNow);
        await _bus.PublishProgressAsync(sent);

        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(sent.JobId, got.JobId);
        Assert.Equal(42, got.Percent);
    }

    [Fact]
    public async Task Cancel_channel_works()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var received = new TaskCompletionSource<Guid>();
        var target = Guid.NewGuid();

        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var id in _bus.SubscribeCancelAsync(cts.Token))
            {
                received.TrySetResult(id);
                break;
            }
        });

        await Task.Delay(100);
        await _bus.PublishCancelAsync(target);

        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(target, got);
    }

    [Fact]
    public async Task FanOut_two_subscribers_both_receive()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var r1 = new TaskCompletionSource<JobResultEvent>();
        var r2 = new TaskCompletionSource<JobResultEvent>();

        _ = Task.Run(async () =>
        {
            await foreach (var e in _bus.SubscribeResultAsync(cts.Token)) { r1.TrySetResult(e); break; }
        });
        _ = Task.Run(async () =>
        {
            await foreach (var e in _bus.SubscribeResultAsync(cts.Token)) { r2.TrySetResult(e); break; }
        });

        await Task.Delay(150);
        await _bus.PublishResultAsync(new JobResultEvent(Guid.NewGuid(),
            JobTerminalState.Completed, null, null, DateTimeOffset.UtcNow));

        await Task.WhenAll(r1.Task.WaitAsync(TimeSpan.FromSeconds(3)),
                            r2.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }
}
