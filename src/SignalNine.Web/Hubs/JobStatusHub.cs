using System.Text.Json;

using Microsoft.AspNetCore.SignalR;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Web.Hubs;

public class JobStatusHub : Hub
{
    private readonly IJobManager _jobManager;

    public JobStatusHub(IJobManager jobManager)
    {
        ArgumentNullException.ThrowIfNull(jobManager);

        _jobManager = jobManager;
    }

    public async Task<JobResponse> EnqueueJob(EnqueueJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new HubException("Type is required.");
        }

        var command = new EnqueueJobCommand
        {
            Type = request.Type,
            PayloadJson = request.Payload.ValueKind == JsonValueKind.Undefined
                              ? "{}"
                              : request.Payload.GetRawText()
        };
        var job = await _jobManager.EnqueueAsync(command, Context.ConnectionAborted).ConfigureAwait(false);

        return ToResponse(job);
    }

    public Task<bool> CancelJob(Guid jobId)
        => _jobManager.CancelAsync(jobId, Context.ConnectionAborted);

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
}
