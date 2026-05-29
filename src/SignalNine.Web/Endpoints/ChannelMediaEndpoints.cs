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

    private static async Task<IResult> Stream(
        Guid id,
        HttpContext ctx,
        IDataAccess<ChannelMediaEntity> media,
        IDataAccess<MediaLibraryEntity> libraries,
        IJellyfinConnectionService jellyfinConnection,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken
    )
    {
        var entity = media.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        if (entity.SourceType == MediaSourceType.Jellyfin)
        {
            if (string.IsNullOrWhiteSpace(entity.SourceRef))
            {
                return TypedResults.NotFound();
            }
            var creds = await jellyfinConnection.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
            if (creds is null)
            {
                return TypedResults.Problem(
                    detail: "Jellyfin connection is not configured.",
                    statusCode: 503
                );
            }
            var baseUrl = creds.Value.BaseUrl.TrimEnd('/');
            var upstream =
                $"{baseUrl}/Videos/{Uri.EscapeDataString(entity.SourceRef)}/stream?static=true"
                + $"&api_key={Uri.EscapeDataString(creds.Value.ApiKey)}";
            await ProxyAsync(ctx, httpClientFactory, upstream, cancellationToken).ConfigureAwait(false);
            return Results.Empty;
        }

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

    private static async Task ProxyAsync(
        HttpContext ctx,
        IHttpClientFactory httpClientFactory,
        string upstream,
        CancellationToken cancellationToken
    )
    {
        var client = httpClientFactory.CreateClient("jellyfin-stream");

        using var req = new HttpRequestMessage(HttpMethod.Get, upstream);

        // Forward Range / If-Range / If-Modified-Since headers so the browser
        // can seek without buffering the whole video first.
        if (ctx.Request.Headers.TryGetValue("Range", out var range))
        {
            req.Headers.TryAddWithoutValidation("Range", (string[])range!);
        }
        if (ctx.Request.Headers.TryGetValue("If-Range", out var ifRange))
        {
            req.Headers.TryAddWithoutValidation("If-Range", (string[])ifRange!);
        }
        if (ctx.Request.Headers.TryGetValue("If-Modified-Since", out var ims))
        {
            req.Headers.TryAddWithoutValidation("If-Modified-Since", (string[])ims!);
        }

        using var resp = await client
            .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        // Translate upstream 4xx-on-missing-media into a clearer 404 so the UI can show
        // "media file not available" instead of leaking openresty's bare "Bad Request".
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest
            || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsync(
                "{\"title\":\"Media file not available\",\"detail\":\"The Jellyfin server does not have a streamable file for this item.\",\"status\":404}",
                cancellationToken
            ).ConfigureAwait(false);
            return;
        }

        ctx.Response.StatusCode = (int)resp.StatusCode;

        foreach (var h in resp.Headers)
        {
            if (IsHopByHopHeader(h.Key)) continue;
            ctx.Response.Headers[h.Key] = h.Value.ToArray();
        }
        foreach (var h in resp.Content.Headers)
        {
            if (IsHopByHopHeader(h.Key)) continue;
            ctx.Response.Headers[h.Key] = h.Value.ToArray();
        }
        // Aspnet sets transfer-encoding chunked when we stream; let it manage.
        ctx.Response.Headers.Remove("transfer-encoding");

        await resp.Content.CopyToAsync(ctx.Response.Body, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsHopByHopHeader(string name)
    {
        return string.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Keep-Alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "TE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Trailer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Upgrade", StringComparison.OrdinalIgnoreCase);
    }

    private static Ok<IReadOnlyList<ChannelMediaResponse>> List(
        IDataAccess<ChannelMediaEntity> dataAccess,
        IDataAccess<TagEntity> tagAccess,
        IDataAccess<ChannelMediaTagEntity> joinAccess,
        ChannelMediaType? type
    )
    {
        IEnumerable<ChannelMediaEntity> query = dataAccess.List();
        if (type is not null) query = query.Where(m => m.Type == type.Value);

        var tagsById = tagAccess.List().ToDictionary(t => t.Id);
        var tagsByMedia = joinAccess
            .List()
            .GroupBy(j => j.ChannelMediaId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TagSummary>)g
                    .Where(j => tagsById.ContainsKey(j.TagId))
                    .Select(j =>
                    {
                        var t = tagsById[j.TagId];
                        return new TagSummary(t.Id, t.Name, t.Label);
                    })
                    .ToList()
            );

        return TypedResults.Ok<IReadOnlyList<ChannelMediaResponse>>(
            query
                .Select(m => ToResponse(m, tagsByMedia.GetValueOrDefault(m.Id) ?? Array.Empty<TagSummary>()))
                .ToList()
        );
    }

    private static Results<Ok<ChannelMediaResponse>, NotFound> GetById(
        Guid id,
        IDataAccess<ChannelMediaEntity> dataAccess,
        IDataAccess<TagEntity> tagAccess,
        IDataAccess<ChannelMediaTagEntity> joinAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();
        return TypedResults.Ok(ToResponse(entity, TagsFor(id, tagAccess, joinAccess)));
    }

    private static IReadOnlyList<TagSummary> TagsFor(
        Guid mediaId,
        IDataAccess<TagEntity> tagAccess,
        IDataAccess<ChannelMediaTagEntity> joinAccess
    )
    {
        var tagIds = joinAccess
            .List()
            .Where(j => j.ChannelMediaId == mediaId)
            .Select(j => j.TagId)
            .ToHashSet();
        if (tagIds.Count == 0) return Array.Empty<TagSummary>();
        return tagAccess
            .List()
            .Where(t => tagIds.Contains(t.Id))
            .Select(t => new TagSummary(t.Id, t.Name, t.Label))
            .ToList();
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

    private static ChannelMediaResponse ToResponse(
        ChannelMediaEntity e,
        IReadOnlyList<TagSummary>? tags = null
    ) =>
        new(
            e.Id, e.Type, e.Title, e.DurationSeconds, e.IsActive,
            e.SourceType, e.SourceRef,
            e.MovieReleaseYear, e.MovieDirector,
            e.TvSeriesName, e.TvSeason, e.TvEpisode,
            e.CommercialAdvertiser, e.CommercialCampaign,
            e.InformationEdition,
            e.CreatedAt, e.UpdatedAt,
            tags ?? Array.Empty<TagSummary>()
        );
}
