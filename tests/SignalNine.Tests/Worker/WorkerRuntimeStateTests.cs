// tests/SignalNine.Tests/Worker/WorkerRuntimeStateTests.cs
using SignalNine.Core.Data.Config;
using SignalNine.Worker.Services;

namespace SignalNine.Tests.Worker;

public class WorkerRuntimeStateTests
{
    private static WorkerRuntimeState NewState(int capacity = 4)
    {
        var config = new SignalNineConfig { JobSystem = new JobSystemConfig { MaxConcurrentJobs = capacity } };
        return new WorkerRuntimeState(config);
    }

    [Fact]
    public void InitialState_HasCorrectCapacityAndZeroRunning()
    {
        var state = NewState(4);

        Assert.Equal(4, state.Capacity);
        Assert.Equal(0, state.RunningCount);
        Assert.Empty(state.Snapshot());
    }

    [Fact]
    public void MarkStarted_AddsJobToRunning()
    {
        var state = NewState();
        var g1 = Guid.NewGuid();

        state.MarkStarted(g1);

        Assert.Equal(1, state.RunningCount);
        Assert.Contains(g1, state.Snapshot());
    }

    [Fact]
    public void MarkStarted_SameId_IsIdempotent()
    {
        var state = NewState();
        var g1 = Guid.NewGuid();

        state.MarkStarted(g1);
        state.MarkStarted(g1);

        Assert.Equal(1, state.RunningCount);
    }

    [Fact]
    public void MarkFinished_RemovesJobFromRunning()
    {
        var state = NewState();
        var g1 = Guid.NewGuid();

        state.MarkStarted(g1);
        state.MarkFinished(g1);

        Assert.Equal(0, state.RunningCount);
        Assert.DoesNotContain(g1, state.Snapshot());
    }

    [Fact]
    public void MarkFinished_UnknownId_DoesNotThrow()
    {
        var state = NewState();

        var ex = Record.Exception(() => state.MarkFinished(Guid.NewGuid()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Concurrency_ParallelMarkStarted_TracksAllJobs()
    {
        var state = NewState(200);
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(ids.Select(id => Task.Run(() => state.MarkStarted(id))));

        Assert.Equal(100, state.RunningCount);
    }
}
