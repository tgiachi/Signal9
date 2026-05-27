using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Core.Services;

public class InMemoryJobManagerTests
{
    private const int DefaultMaxLogEntriesPerJob = 500;
    private const int TestMaxLogEntriesPerJob = 2;

    [Fact]
    public async Task EnqueueAsync_ValidCommand_StoresQueuedJob()
    {
        var manager = CreateManager();

        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(JobStateType.Queued, job.State);
        Assert.Equal("test.job", job.Type);
        Assert.Single(manager.List());
    }

    [Fact]
    public async Task ReportProgressAsync_RunningJob_UpdatesProgress()
    {
        var manager = CreateManager();
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        await manager.ReportProgressAsync(job.Id, 42, "Halfway");

        var snapshot = manager.GetById(job.Id);
        Assert.NotNull(snapshot);
        Assert.Equal(42, snapshot.Progress.Percent);
        Assert.Equal("Halfway", snapshot.Progress.Message);
    }

    [Fact]
    public async Task WriteLogAsync_RetainsConfiguredLogEntries()
    {
        var manager = CreateManager(TestMaxLogEntriesPerJob);
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        await manager.WriteLogAsync(job.Id, JobLogLevelType.Information, "first");
        await manager.WriteLogAsync(job.Id, JobLogLevelType.Warning, "second");
        await manager.WriteLogAsync(job.Id, JobLogLevelType.Error, "third");

        var logs = manager.GetLogs(job.Id);
        Assert.Equal(TestMaxLogEntriesPerJob, logs.Count);
        Assert.Equal("second", logs[0].Message);
        Assert.Equal("third", logs[1].Message);
    }

    [Fact]
    public async Task CancelAsync_RunningJob_CancelsTokenAndMarksJobCanceled()
    {
        var manager = CreateManager();
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        var canceled = await manager.CancelAsync(job.Id);

        Assert.True(canceled);
        Assert.True(manager.GetCancellationToken(job.Id).IsCancellationRequested);
        Assert.Equal(JobStateType.Canceled, manager.GetById(job.Id)?.State);
    }

    private static IJobManager CreateManager(int maxLogEntriesPerJob = DefaultMaxLogEntriesPerJob)
        => new InMemoryJobManager(
            new SignalNineConfig
            {
                JobSystem =
                {
                    MaxLogEntriesPerJob = maxLogEntriesPerJob
                }
            },
            new NullJobNotificationPublisher()
        );

    private sealed class NullJobNotificationPublisher : IJobNotificationPublisher
    {
        public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
