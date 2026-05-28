// tests/SignalNine.Tests/Worker/EndToEndWorkerTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Services.Redis;
using SignalNine.Tests.Support;
using SignalNine.Worker.Services;
using StackExchange.Redis;

namespace SignalNine.Tests.Worker;

[Collection(RedisCollection.Name)]
public class EndToEndWorkerTests : IAsyncLifetime
{
    private readonly RedisContainerFixture _redis;
    private readonly string _prefix;
    private RedisStreamKeys _keys = default!;
    private RedisJobQueue _queue = default!;
    private RedisJobBus _bus = default!;

    public EndToEndWorkerTests(RedisContainerFixture redis)
    {
        _redis = redis;
        _prefix = $"e2e:{Guid.NewGuid():N}:";
    }

    public async Task InitializeAsync()
    {
        var cfg = new RedisConfig { Url = _redis.ConnectionString, KeyPrefix = _prefix };
        _keys = new RedisStreamKeys(cfg);
        _queue = new RedisJobQueue(_redis.Connection, _keys);
        _bus = new RedisJobBus(_redis.Connection, _keys);
        await _queue.EnsureConsumerGroupsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class EchoHandler : SignalNine.Core.Interfaces.IJobHandler
    {
        public string Type => "e2e.echo";
        public readonly TaskCompletionSource<JobExecutionContext> Received = new();
        public Task<IJobResult> ExecuteAsync(JobExecutionContext ctx, CancellationToken ct)
        {
            Received.TrySetResult(ctx);
            return Task.FromResult<IJobResult>(new EmptyJobResult(Type));
        }
    }

    [Fact(Timeout = 10_000)]
    public async Task Web_enqueue_then_worker_consume_completes_with_result()
    {
        var handler = new EchoHandler();
        var identity = new WorkerIdentity(Guid.NewGuid(), "e2e-worker");
        var config = new SignalNineConfig { JobSystem = new() { MaxConcurrentJobs = 1 } };
        var state = new WorkerRuntimeState(config);
        var loop = new WorkerJobLoop(config, new[] { handler }, _queue, _bus, identity, state);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var resultTcs = new TaskCompletionSource<JobResultEvent>();
        _ = Task.Run(async () =>
        {
            await foreach (var e in _bus.SubscribeResultAsync(cts.Token))
            { resultTcs.TrySetResult(e); break; }
        });
        await Task.Delay(150);

        var loopTask = loop.StartAsync(cts.Token);
        var jobId = Guid.NewGuid();
        await _queue.PushAsync(new JobEnvelope(jobId, "e2e.echo", "{\"hello\":1}", "/tmp/x", 0, DateTimeOffset.UtcNow),
            JobStreamTarget.Workers);

        var executed = await handler.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("{\"hello\":1}", executed.PayloadJson);

        var result = await resultTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobTerminalState.Completed, result.State);

        await cts.CancelAsync();
        await loopTask;
    }
}
