using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

public interface IJobBus
{
    Task PublishProgressAsync(JobProgressEvent progressEvent, CancellationToken cancellationToken = default);
    Task PublishLogAsync(JobLogEvent logEvent, CancellationToken cancellationToken = default);
    Task PublishResultAsync(JobResultEvent resultEvent, CancellationToken cancellationToken = default);
    Task PublishCancelAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task PublishHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default);

    IAsyncEnumerable<JobProgressEvent> SubscribeProgressAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<JobLogEvent> SubscribeLogAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<JobResultEvent> SubscribeResultAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<Guid> SubscribeCancelAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<WorkerHeartbeat> SubscribeHeartbeatAsync(CancellationToken cancellationToken);
}
