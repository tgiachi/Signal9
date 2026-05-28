using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Tests.Support.Jobs;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web;

public class JobWorkerServiceTests
{
    private const int MaxConcurrentJobs = 1;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ExecuteAsync_MaxConcurrentJobsOne_RunsOneJobAtATime()
    {
        var handler = new FakeJobHandler();
        var manager = new InMemoryJobManager(CreateConfig(), new NoOpJobNotificationPublisher(), new InMemoryJobQueue(), new JobTypeRouter());
        var service = new JobWorkerService(CreateConfig(), [handler], manager);
        using var timeoutSource = new CancellationTokenSource(TestTimeout);

        await service.StartAsync(timeoutSource.Token);
        var firstJob = await manager.EnqueueAsync(new EnqueueJobCommand { Type = handler.Type });
        var secondJob = await manager.EnqueueAsync(new EnqueueJobCommand { Type = handler.Type });
        await handler.WaitForStartedCountAsync(1, timeoutSource.Token);

        Assert.Equal(JobStateType.Running, manager.GetById(firstJob.Id)?.State);
        Assert.Equal(JobStateType.Queued, manager.GetById(secondJob.Id)?.State);

        handler.Release.SetResult();
        await handler.WaitForStartedCountAsync(2, timeoutSource.Token);
        await service.StopAsync(timeoutSource.Token);

        Assert.Equal(JobStateType.Completed, manager.GetById(firstJob.Id)?.State);
        Assert.Equal(JobStateType.Completed, manager.GetById(secondJob.Id)?.State);
    }

    private static SignalNineConfig CreateConfig()
        => new()
        {
            JobSystem =
            {
                MaxConcurrentJobs = MaxConcurrentJobs
            }
        };
}
