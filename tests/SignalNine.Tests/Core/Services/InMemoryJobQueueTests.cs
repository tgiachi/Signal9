// tests/SignalNine.Tests/Core/Services/InMemoryJobQueueTests.cs
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class InMemoryJobQueueTests
{
    private static JobEnvelope MakeEnvelope(string type = "media.pipeline")
        => new(Guid.NewGuid(), type, "{}", "/tmp/work", 0, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Push_then_Pull_returns_envelope()
    {
        var queue = new InMemoryJobQueue();
        var env = MakeEnvelope();
        await queue.PushAsync(env, JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var queued = await queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);

        Assert.NotNull(queued);
        Assert.Equal(env.JobId, queued!.Envelope.JobId);
    }

    [Fact]
    public async Task Pull_blocks_until_push()
    {
        var queue = new InMemoryJobQueue();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var pullTask = queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);
        Assert.False(pullTask.IsCompleted);

        await queue.PushAsync(MakeEnvelope(), JobStreamTarget.Workers);
        var queued = await pullTask;
        Assert.NotNull(queued);
    }

    [Fact]
    public async Task Targets_are_independent_streams()
    {
        var queue = new InMemoryJobQueue();
        await queue.PushAsync(MakeEnvelope("library.scan"), JobStreamTarget.Internal);
        await queue.PushAsync(MakeEnvelope("media.pipeline"), JobStreamTarget.Workers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var internalJob = await queue.PullAsync("c1", JobStreamTarget.Internal, cts.Token);
        var workersJob = await queue.PullAsync("c2", JobStreamTarget.Workers, cts.Token);

        Assert.Equal("library.scan", internalJob!.Envelope.Type);
        Assert.Equal("media.pipeline", workersJob!.Envelope.Type);
    }

    [Fact]
    public async Task Ack_is_no_op_in_memory()
    {
        var queue = new InMemoryJobQueue();
        await queue.AckAsync("any-id", JobStreamTarget.Workers); // should not throw
    }

    [Fact]
    public async Task RequeueLater_re_enqueues_after_delay()
    {
        var queue = new InMemoryJobQueue();
        var env = MakeEnvelope();
        var start = DateTimeOffset.UtcNow;
        await queue.RequeueLaterAsync(env, TimeSpan.FromMilliseconds(100));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var queued = await queue.PullAsync("c1", JobStreamTarget.Workers, cts.Token);

        Assert.NotNull(queued);
        Assert.True(DateTimeOffset.UtcNow - start >= TimeSpan.FromMilliseconds(90));
    }
}
