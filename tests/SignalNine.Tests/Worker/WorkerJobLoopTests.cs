// tests/SignalNine.Tests/Worker/WorkerJobLoopTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Worker.Services;

namespace SignalNine.Tests.Worker;

public class WorkerJobLoopTests
{
    private sealed class StubHandler : IJobHandler
    {
        private readonly Func<JobExecutionContext, CancellationToken, Task<IJobResult>> _impl;
        public StubHandler(string type, Func<JobExecutionContext, CancellationToken, Task<IJobResult>> impl)
        { Type = type; _impl = impl; }
        public string Type { get; }
        public Task<IJobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct) => _impl(context, ct);
    }

    private static WorkerJobLoop NewLoop(IJobQueue queue, IJobBus bus, IEnumerable<IJobHandler> handlers, int concurrency = 1)
    {
        var identity = new WorkerIdentity(Guid.NewGuid(), "test");
        var config = new SignalNineConfig { JobSystem = new() { MaxConcurrentJobs = concurrency } };
        var state = new WorkerRuntimeState(config);
        return new WorkerJobLoop(config, handlers, queue, bus, identity, state);
    }

    [Fact]
    public async Task Loop_pulls_job_from_workers_stream_and_invokes_handler()
    {
        var queue = new InMemoryJobQueue();
        var bus = new InMemoryJobBus();
        var executed = new TaskCompletionSource<JobExecutionContext>();
        var handler = new StubHandler("test.echo", (ctx, _) =>
        {
            executed.TrySetResult(ctx);
            return Task.FromResult<IJobResult>(new EmptyJobResult("test.echo"));
        });

        var loop = NewLoop(queue, bus, new[] { handler });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var loopTask = loop.StartAsync(cts.Token);

        await queue.PushAsync(new JobEnvelope(Guid.NewGuid(), "test.echo", "{}", "/tmp/x", 0, DateTimeOffset.UtcNow),
            JobStreamTarget.Workers);

        var ctx = await executed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("{}", ctx.PayloadJson);

        await cts.CancelAsync();
        await loopTask;
    }

    [Fact]
    public async Task Loop_publishes_result_completed_on_success()
    {
        var queue = new InMemoryJobQueue();
        var bus = new InMemoryJobBus();
        var resultTcs = new TaskCompletionSource<JobResultEvent>();
        var handler = new StubHandler("test.ok", (_, __) => Task.FromResult<IJobResult>(new EmptyJobResult("test.ok")));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var subscribe = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeResultAsync(cts.Token))
            { resultTcs.TrySetResult(e); break; }
        });

        var loop = NewLoop(queue, bus, new[] { handler });
        var loopTask = loop.StartAsync(cts.Token);

        await Task.Delay(80); // let subscribe wire up
        var jobId = Guid.NewGuid();
        await queue.PushAsync(new JobEnvelope(jobId, "test.ok", "{}", "/tmp/x", 0, DateTimeOffset.UtcNow),
            JobStreamTarget.Workers);

        var result = await resultTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(JobTerminalState.Completed, result.State);

        await cts.CancelAsync();
        await loopTask;
    }

    [Fact]
    public async Task Loop_publishes_result_failed_on_handler_exception()
    {
        var queue = new InMemoryJobQueue();
        var bus = new InMemoryJobBus();
        var resultTcs = new TaskCompletionSource<JobResultEvent>();
        var handler = new StubHandler("test.boom", (_, __) => throw new InvalidOperationException("kaboom"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeResultAsync(cts.Token))
            { resultTcs.TrySetResult(e); break; }
        });

        var loop = NewLoop(queue, bus, new[] { handler });
        var loopTask = loop.StartAsync(cts.Token);

        await Task.Delay(80);
        await queue.PushAsync(new JobEnvelope(Guid.NewGuid(), "test.boom", "{}", "/tmp/x", 0, DateTimeOffset.UtcNow),
            JobStreamTarget.Workers);

        var result = await resultTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(JobTerminalState.Failed, result.State);
        Assert.Contains("kaboom", result.Error);

        await cts.CancelAsync();
        await loopTask;
    }

    [Fact]
    public async Task Loop_skips_jobs_with_no_registered_handler()
    {
        var queue = new InMemoryJobQueue();
        var bus = new InMemoryJobBus();
        var resultTcs = new TaskCompletionSource<JobResultEvent>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeResultAsync(cts.Token))
            { resultTcs.TrySetResult(e); break; }
        });

        var loop = NewLoop(queue, bus, Array.Empty<IJobHandler>());
        var loopTask = loop.StartAsync(cts.Token);

        await Task.Delay(80);
        await queue.PushAsync(new JobEnvelope(Guid.NewGuid(), "type.no.handler", "{}", "/tmp/x", 0, DateTimeOffset.UtcNow),
            JobStreamTarget.Workers);

        var result = await resultTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(JobTerminalState.Failed, result.State);
        Assert.Contains("no handler", result.Error, StringComparison.OrdinalIgnoreCase);

        await cts.CancelAsync();
        await loopTask;
    }
}
