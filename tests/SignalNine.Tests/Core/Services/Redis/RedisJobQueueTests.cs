// tests/SignalNine.Tests/Core/Services/Redis/RedisJobQueueTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services.Redis;
using SignalNine.Tests.Support;

namespace SignalNine.Tests.Core.Services.Redis;

[Collection(RedisCollection.Name)]
public class RedisJobQueueTests : IAsyncLifetime
{
    private readonly RedisContainerFixture _redis;
    private readonly string _prefix;
    private RedisJobQueue _queue = default!;

    public RedisJobQueueTests(RedisContainerFixture redis)
    {
        _redis = redis;
        _prefix = $"test:{Guid.NewGuid():N}:";
    }

    public async Task InitializeAsync()
    {
        var cfg = new RedisConfig { Url = _redis.ConnectionString, KeyPrefix = _prefix };
        _queue = new RedisJobQueue(_redis.Connection, new RedisStreamKeys(cfg));
        await _queue.EnsureConsumerGroupsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static JobEnvelope MakeEnvelope(string type = "media.pipeline")
        => new(Guid.NewGuid(), type, "{}", "/tmp/work", 0, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Push_then_Pull_returns_envelope()
    {
        var env = MakeEnvelope();
        await _queue.PushAsync(env, JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var queued = await _queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);

        Assert.NotNull(queued);
        Assert.Equal(env.JobId, queued!.Envelope.JobId);
    }

    [Fact]
    public async Task Two_consumers_in_same_group_split_work()
    {
        for (var i = 0; i < 4; i++)
            await _queue.PushAsync(MakeEnvelope(), JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var c1 = new List<QueuedJob>();
        var c2 = new List<QueuedJob>();
        for (var i = 0; i < 2; i++)
        {
            var a = await _queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);
            var b = await _queue.PullAsync("c2", JobStreamTarget.Workers, cts.Token);
            if (a is not null) c1.Add(a);
            if (b is not null) c2.Add(b);
        }

        Assert.Equal(4, c1.Count + c2.Count);
        Assert.True(c1.Count > 0 && c2.Count > 0);
        Assert.Empty(c1.Select(q => q.Envelope.JobId).Intersect(c2.Select(q => q.Envelope.JobId)));
    }

    [Fact]
    public async Task Ack_removes_from_pending_entries_list()
    {
        var env = MakeEnvelope();
        await _queue.PushAsync(env, JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var queued = await _queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);
        Assert.NotNull(queued);

        await _queue.AckAsync(queued!.StreamId, JobStreamTarget.Workers);

        var pending = await _redis.Connection.GetDatabase()
            .StreamPendingAsync($"{_prefix}jobs:workers", "workers");
        Assert.Equal(0, pending.PendingMessageCount);
    }

    [Fact]
    public async Task Pull_with_no_messages_returns_null_within_block_window()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await _queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);
        Assert.Null(result);
    }

    [Fact]
    public async Task Targets_use_independent_streams()
    {
        await _queue.PushAsync(MakeEnvelope("library.scan"), JobStreamTarget.Internal);
        await _queue.PushAsync(MakeEnvelope("media.pipeline"), JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var fromInternal = await _queue.PullAsync("c", JobStreamTarget.Internal, cts.Token);
        var fromWorkers = await _queue.PullAsync("c", JobStreamTarget.Workers, cts.Token);

        Assert.Equal("library.scan", fromInternal!.Envelope.Type);
        Assert.Equal("media.pipeline", fromWorkers!.Envelope.Type);
    }
}
