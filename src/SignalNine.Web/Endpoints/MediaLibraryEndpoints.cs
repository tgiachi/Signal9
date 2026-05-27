using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Channels;
using SignalNine.Web.Data.Jobs;
using SignalNine.Web.Services;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps MediaLibrary CRUD endpoints under <c>/api/media-libraries</c>.
/// </summary>
public static class MediaLibraryEndpoints
{
    public static WebApplication MapMediaLibraryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/media-libraries").RequireAuthorization();

        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost(string.Empty, Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/scan", Scan);

        return app;
    }

    private static Ok<IReadOnlyList<MediaLibraryResponse>> List(IDataAccess<MediaLibraryEntity> dataAccess)
    {
        var items = dataAccess.List().Select(ToResponse).ToList();
        return TypedResults.Ok<IReadOnlyList<MediaLibraryResponse>>(items);
    }

    private static Results<Ok<MediaLibraryResponse>, NotFound> GetById(
        Guid id,
        IDataAccess<MediaLibraryEntity> dataAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        return entity is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static Results<Created<MediaLibraryResponse>, BadRequest<string>, Conflict<string>> Create(
        CreateMediaLibraryRequest request,
        IDataAccess<MediaLibraryEntity> dataAccess
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SourceRef))
        {
            return TypedResults.BadRequest("Name and SourceRef are required.");
        }

        if (dataAccess.List().Any(l => l.SourceType == request.SourceType && l.SourceRef == request.SourceRef))
        {
            return TypedResults.Conflict(
                $"A library with ({request.SourceType}, {request.SourceRef}) already exists."
            );
        }

        var entity = new MediaLibraryEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            DefaultMediaType = request.DefaultMediaType,
            SourceType = request.SourceType,
            SourceRef = request.SourceRef,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dataAccess.Insert(entity);
        return TypedResults.Created($"/api/media-libraries/{entity.Id}", ToResponse(entity));
    }

    private static Results<Ok<MediaLibraryResponse>, NotFound, BadRequest<string>, Conflict<string>> Update(
        Guid id,
        UpdateMediaLibraryRequest request,
        IDataAccess<MediaLibraryEntity> dataAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SourceRef))
        {
            return TypedResults.BadRequest("Name and SourceRef are required.");
        }

        var sourceChanged = entity.SourceType != request.SourceType || entity.SourceRef != request.SourceRef;
        if (sourceChanged
            && dataAccess.List().Any(l => l.Id != id
                                          && l.SourceType == request.SourceType
                                          && l.SourceRef == request.SourceRef))
        {
            return TypedResults.Conflict(
                $"A library with ({request.SourceType}, {request.SourceRef}) already exists."
            );
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.DefaultMediaType = request.DefaultMediaType;
        entity.IsActive = request.IsActive;
        entity.SourceType = request.SourceType;
        entity.SourceRef = request.SourceRef;
        entity.UpdatedAt = DateTime.UtcNow;

        dataAccess.Update(entity);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static Results<NoContent, NotFound> Delete(Guid id, IDataAccess<MediaLibraryEntity> dataAccess)
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        dataAccess.Delete(entity.Id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Accepted<JobResponse>, NotFound, Conflict<string>>> Scan(
        Guid id,
        IDataAccess<MediaLibraryEntity> dataAccess,
        IJobManager jobs,
        CancellationToken ct
    )
    {
        var library = dataAccess.GetByKey(id);
        if (library is null) return TypedResults.NotFound();
        if (!library.IsActive) return TypedResults.Conflict("Library is inactive.");

        var payload = JsonSerializer.Serialize(new ScanLibraryPayload(id));
        var snapshot = await jobs.EnqueueAsync(new EnqueueJobCommand
        {
            Type = LibraryScanJobHandler.JobType,
            PayloadJson = payload
        }, ct);

        return TypedResults.Accepted($"/api/jobs/{snapshot.Id}", ToJobResponse(snapshot));
    }

    private static JobResponse ToJobResponse(JobSnapshot job)
    {
        return new(
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

    private static MediaLibraryResponse ToResponse(MediaLibraryEntity e)
    {
        return new(
            e.Id, e.Name, e.Description, e.DefaultMediaType, e.SourceType, e.SourceRef,
            e.IsActive, e.LastScannedAt, e.CreatedAt, e.UpdatedAt
        );
    }
}
