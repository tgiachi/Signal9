using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps Tag endpoints under <c>/api/tags</c>.
/// </summary>
public static class TagEndpoints
{
    public static WebApplication MapTagEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tags").RequireAuthorization();

        group.MapGet(string.Empty, List);
        group.MapPost(string.Empty, Create);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static Ok<IReadOnlyList<TagResponse>> List(IDataAccess<TagEntity> dataAccess)
    {
        var items = dataAccess.List().Select(ToResponse).ToList();
        return TypedResults.Ok<IReadOnlyList<TagResponse>>(items);
    }

    private static Results<Created<TagResponse>, BadRequest<string>> Create(
        CreateTagRequest request,
        IDataAccess<TagEntity> dataAccess
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest("Name is required.");
        }

        var normalized = request.Name.Trim().ToLowerInvariant();

        var existing = dataAccess.List().FirstOrDefault(t => t.Name == normalized);
        if (existing is not null)
        {
            return TypedResults.Created($"/api/tags/{existing.Id}", ToResponse(existing));
        }

        var entity = new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            Label = request.Label,
            CreatedAt = DateTime.UtcNow
        };

        dataAccess.Insert(entity);
        return TypedResults.Created($"/api/tags/{entity.Id}", ToResponse(entity));
    }

    private static Results<NoContent, NotFound> Delete(
        Guid id,
        IDataAccess<TagEntity> dataAccess,
        IDataAccess<ChannelMediaTagEntity> joinAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        var joins = joinAccess.List().Where(j => j.TagId == id).ToList();
        foreach (var join in joins) joinAccess.Delete(join.Id);

        dataAccess.Delete(entity.Id);
        return TypedResults.NoContent();
    }

    private static TagResponse ToResponse(TagEntity e) => new(e.Id, e.Name, e.Label, e.CreatedAt);
}
