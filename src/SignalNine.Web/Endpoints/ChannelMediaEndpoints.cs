using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Web.Data.Channels;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps ChannelMedia CRUD + tag attach/detach endpoints under <c>/api/media</c>.
/// </summary>
public static class ChannelMediaEndpoints
{
    public static WebApplication MapChannelMediaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/media").RequireAuthorization();

        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost(string.Empty, Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        group.MapPost("/{id:guid}/tags/{tagId:guid}", AttachTag);
        group.MapDelete("/{id:guid}/tags/{tagId:guid}", DetachTag);

        group.MapPost("/{id:guid}/pipeline", Pipeline);
        group.MapGet("/{id:guid}/stream", Stream);

        return app;
    }

    private static IResult Stream(
        Guid id,
        IDataAccess<ChannelMediaEntity> media,
        IDataAccess<MediaLibraryEntity> libraries
    )
    {
        var entity = media.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();
        if (entity.SourceType != MediaSourceType.LocalFile)
        {
            return TypedResults.Problem(
                detail: $"Streaming not supported for source type {entity.SourceType}.",
                statusCode: 501
            );
        }

        var library = libraries.GetByKey(entity.MediaLibraryId);
        if (library is null) return TypedResults.NotFound();
        if (string.IsNullOrWhiteSpace(library.SourceRef) || string.IsNullOrWhiteSpace(entity.SourceRef))
        {
            return TypedResults.NotFound();
        }

        var root = Path.GetFullPath(library.SourceRef);
        var fullPath = Path.GetFullPath(Path.Combine(root, entity.SourceRef));
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest("invalid path");
        }
        if (!File.Exists(fullPath)) return TypedResults.NotFound();

        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fullPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return Results.File(fullPath, contentType, enableRangeProcessing: true);
    }

    private static Ok<IReadOnlyList<ChannelMediaResponse>> List(
        IDataAccess<ChannelMediaEntity> dataAccess,
        ChannelMediaType? type
    )
    {
        IEnumerable<ChannelMediaEntity> query = dataAccess.List();
        if (type is not null) query = query.Where(m => m.Type == type.Value);
        return TypedResults.Ok<IReadOnlyList<ChannelMediaResponse>>(query.Select(ToResponse).ToList());
    }

    private static Results<Ok<ChannelMediaResponse>, NotFound> GetById(Guid id, IDataAccess<ChannelMediaEntity> dataAccess)
    {
        var entity = dataAccess.GetByKey(id);
        return entity is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static Results<Created<ChannelMediaResponse>, BadRequest<string>> Create(
        CreateChannelMediaRequest request,
        IDataAccess<ChannelMediaEntity> dataAccess
    )
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest("Title is required.");
        }

        var entity = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Title = request.Title,
            DurationSeconds = request.DurationSeconds,
            IsActive = true,
            SourceType = request.SourceType,
            SourceRef = request.SourceRef,
            MovieReleaseYear = request.MovieReleaseYear,
            MovieDirector = request.MovieDirector,
            TvSeriesName = request.TvSeriesName,
            TvSeason = request.TvSeason,
            TvEpisode = request.TvEpisode,
            CommercialAdvertiser = request.CommercialAdvertiser,
            CommercialCampaign = request.CommercialCampaign,
            InformationEdition = request.InformationEdition,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dataAccess.Insert(entity);
        return TypedResults.Created($"/api/media/{entity.Id}", ToResponse(entity));
    }

    private static Results<Ok<ChannelMediaResponse>, NotFound, BadRequest<string>> Update(
        Guid id,
        UpdateChannelMediaRequest request,
        IDataAccess<ChannelMediaEntity> dataAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest("Title is required.");
        }

        entity.Type = request.Type;
        entity.Title = request.Title;
        entity.DurationSeconds = request.DurationSeconds;
        entity.IsActive = request.IsActive;
        entity.SourceType = request.SourceType;
        entity.SourceRef = request.SourceRef;
        entity.MovieReleaseYear = request.MovieReleaseYear;
        entity.MovieDirector = request.MovieDirector;
        entity.TvSeriesName = request.TvSeriesName;
        entity.TvSeason = request.TvSeason;
        entity.TvEpisode = request.TvEpisode;
        entity.CommercialAdvertiser = request.CommercialAdvertiser;
        entity.CommercialCampaign = request.CommercialCampaign;
        entity.InformationEdition = request.InformationEdition;
        entity.UpdatedAt = DateTime.UtcNow;

        dataAccess.Update(entity);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static Results<NoContent, NotFound> Delete(
        Guid id,
        IDataAccess<ChannelMediaEntity> dataAccess,
        IDataAccess<ChannelMediaTagEntity> tagJoinAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        var joins = tagJoinAccess.List().Where(j => j.ChannelMediaId == id).ToList();
        foreach (var join in joins) tagJoinAccess.Delete(join.Id);

        dataAccess.Delete(entity.Id);
        return TypedResults.NoContent();
    }

    private static Results<NoContent, NotFound, Conflict<string>> AttachTag(
        Guid id,
        Guid tagId,
        IDataAccess<ChannelMediaEntity> mediaAccess,
        IDataAccess<TagEntity> tagAccess,
        IDataAccess<ChannelMediaTagEntity> joinAccess
    )
    {
        if (mediaAccess.GetByKey(id) is null) return TypedResults.NotFound();
        if (tagAccess.GetByKey(tagId) is null) return TypedResults.NotFound();

        if (joinAccess.List().Any(j => j.ChannelMediaId == id && j.TagId == tagId))
        {
            return TypedResults.Conflict("Tag is already attached to this media.");
        }

        joinAccess.Insert(
            new ChannelMediaTagEntity
            {
                Id = Guid.NewGuid(),
                ChannelMediaId = id,
                TagId = tagId,
                CreatedAt = DateTime.UtcNow
            }
        );

        return TypedResults.NoContent();
    }

    private static Results<NoContent, NotFound> DetachTag(
        Guid id,
        Guid tagId,
        IDataAccess<ChannelMediaTagEntity> joinAccess
    )
    {
        var join = joinAccess.List().FirstOrDefault(j => j.ChannelMediaId == id && j.TagId == tagId);
        if (join is null) return TypedResults.NotFound();

        joinAccess.Delete(join.Id);
        return TypedResults.NoContent();
    }

    // MediaPipelineJobHandler.JobType — kept as literal here to avoid a tight coupling.
    private const string PipelineJobType = "media.pipeline";

    private static async Task<Results<Accepted<JobResponse>, NotFound, Conflict<string>>> Pipeline(
        Guid id,
        IDataAccess<ChannelMediaEntity> dataAccess,
        IJobManager jobs,
        CancellationToken ct
    )
    {
        var media = dataAccess.GetByKey(id);
        if (media is null)
        {
            return TypedResults.NotFound();
        }
        if (!media.IsActive)
        {
            return TypedResults.Conflict("Media is inactive.");
        }

        var payload = JsonSerializer.Serialize(new MediaPipelinePayload(id));
        var snapshot = await jobs.EnqueueAsync(new EnqueueJobCommand
        {
            Type = PipelineJobType,
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

    private static ChannelMediaResponse ToResponse(ChannelMediaEntity e) =>
        new(
            e.Id, e.Type, e.Title, e.DurationSeconds, e.IsActive,
            e.SourceType, e.SourceRef,
            e.MovieReleaseYear, e.MovieDirector,
            e.TvSeriesName, e.TvSeason, e.TvEpisode,
            e.CommercialAdvertiser, e.CommercialCampaign,
            e.InformationEdition,
            e.CreatedAt, e.UpdatedAt
        );
}
