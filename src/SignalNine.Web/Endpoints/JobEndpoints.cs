using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps job-related HTTP endpoints.
/// </summary>
public static class JobEndpoints
{
    /// <summary>
    /// Maps job endpoints under <c>/api/jobs</c>.
    /// </summary>
    public static WebApplication MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").RequireAuthorization();

        group.MapGet(
            "/",
            ListJobs
        );

        group.MapGet(
            "/{jobId:guid}",
            GetJob
        );

        group.MapGet(
            "/{jobId:guid}/logs",
            GetJobLogs
        );

        group.MapPost(
            "/",
            async Task<Results<Accepted<JobResponse>, BadRequest<string>>> (
                    EnqueueJobRequest request,
                    IJobManager manager,
                    CancellationToken cancellationToken
                ) =>
                await EnqueueJobAsync(request, manager, cancellationToken).ConfigureAwait(false)
        );

        group.MapPost(
            "/{jobId:guid}/cancel",
            async Task<Results<Accepted, NotFound>> (Guid jobId, IJobManager manager, CancellationToken cancellationToken) =>
                await CancelJobAsync(jobId, manager, cancellationToken).ConfigureAwait(false)
        );

        return app;
    }

    private static Results<Ok<IReadOnlyList<JobResponse>>, NotFound> ListJobs(IJobManager manager)
    {
        var jobs = manager.List().Select(ToResponse).ToList();

        return TypedResults.Ok<IReadOnlyList<JobResponse>>(jobs);
    }

    private static Results<Ok<JobResponse>, NotFound> GetJob(Guid jobId, IJobManager manager)
    {
        var job = manager.GetById(jobId);

        return job is null
                   ? TypedResults.NotFound()
                   : TypedResults.Ok(ToResponse(job));
    }

    private static Results<Ok<IReadOnlyList<JobLogResponse>>, NotFound> GetJobLogs(Guid jobId, IJobManager manager)
    {
        var job = manager.GetById(jobId);

        if (job is null)
        {
            return TypedResults.NotFound();
        }

        var logs = manager.GetLogs(jobId).Select(ToLogResponse).ToList();

        return TypedResults.Ok<IReadOnlyList<JobLogResponse>>(logs);
    }

    private static async Task<Results<Accepted<JobResponse>, BadRequest<string>>> EnqueueJobAsync(
        EnqueueJobRequest request,
        IJobManager manager,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return TypedResults.BadRequest("Type is required.");
        }

        var command = new EnqueueJobCommand
        {
            Type = request.Type,
            PayloadJson = request.Payload.ValueKind == JsonValueKind.Undefined
                              ? "{}"
                              : request.Payload.GetRawText()
        };

        var job = await manager.EnqueueAsync(command, cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted($"/api/jobs/{job.Id}", ToResponse(job));
    }

    private static async Task<Results<Accepted, NotFound>> CancelJobAsync(
        Guid jobId,
        IJobManager manager,
        CancellationToken cancellationToken
    )
    {
        var canceled = await manager.CancelAsync(jobId, cancellationToken).ConfigureAwait(false);

        return canceled ? TypedResults.Accepted($"/api/jobs/{jobId}") : TypedResults.NotFound();
    }

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
