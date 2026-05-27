using Microsoft.AspNetCore.SignalR;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jobs;
using SignalNine.Web.Hubs;

namespace SignalNine.Web.Services;

public class SignalRJobNotificationPublisher : IJobNotificationPublisher
{
    private readonly IHubContext<JobLogHub> _logHub;
    private readonly IHubContext<JobStatusHub> _statusHub;

    public SignalRJobNotificationPublisher(IHubContext<JobStatusHub> statusHub, IHubContext<JobLogHub> logHub)
    {
        ArgumentNullException.ThrowIfNull(statusHub);
        ArgumentNullException.ThrowIfNull(logHub);

        _statusHub = statusHub;
        _logHub = logHub;
    }

    public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
        => _statusHub.Clients.All.SendAsync("JobStatusChanged", ToResponse(snapshot), cancellationToken);

    public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
        => _logHub.Clients.All.SendAsync("JobLogReceived", ToLogResponse(entry), cancellationToken);

    private static JobResponse ToResponse(JobSnapshot job)
        => new(
            job.Id,
            job.Type,
            job.State,
            job.Progress.Percent,
            job.Progress.Message,
            job.Error,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt
        );

    private static JobLogResponse ToLogResponse(JobLogEntry entry)
        => new(entry.Sequence, entry.JobId, entry.Timestamp, entry.Level, entry.Message);
}
