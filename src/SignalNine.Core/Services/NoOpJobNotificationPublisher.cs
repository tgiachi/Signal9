using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public class NoOpJobNotificationPublisher : IJobNotificationPublisher
{
    public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
