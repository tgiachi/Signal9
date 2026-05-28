using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

public interface IJobQueue
{
    Task PushAsync(JobEnvelope envelope, JobStreamTarget target, CancellationToken cancellationToken = default);
    Task<QueuedJob?> PullAsync(string consumerName, JobStreamTarget target, CancellationToken cancellationToken = default);
    Task AckAsync(string streamId, JobStreamTarget target, CancellationToken cancellationToken = default);
    Task RequeueLaterAsync(JobEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken = default);
}
