using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps Channel CRUD endpoints under <c>/api/channels</c>.
/// </summary>
public static class ChannelEndpoints
{
    public static WebApplication MapChannelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels").RequireAuthorization();

        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/by-slug/{slug}", GetBySlug);
        group.MapPost(string.Empty, Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static Ok<IReadOnlyList<ChannelResponse>> List(IDataAccess<ChannelEntity> dataAccess)
    {
        var items = dataAccess.List().Select(ToResponse).ToList();
        return TypedResults.Ok<IReadOnlyList<ChannelResponse>>(items);
    }

    private static Results<Ok<ChannelResponse>, NotFound> GetById(Guid id, IDataAccess<ChannelEntity> dataAccess)
    {
        var entity = dataAccess.GetByKey(id);
        return entity is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static Results<Ok<ChannelResponse>, NotFound> GetBySlug(string slug, IDataAccess<ChannelEntity> dataAccess)
    {
        var entity = dataAccess.List().FirstOrDefault(c => c.Slug == slug);
        return entity is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static Results<Created<ChannelResponse>, BadRequest<string>, Conflict<string>> Create(
        CreateChannelRequest request,
        IDataAccess<ChannelEntity> dataAccess
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
        {
            return TypedResults.BadRequest("Name and Slug are required.");
        }

        if (dataAccess.List().Any(c => c.Slug == request.Slug))
        {
            return TypedResults.Conflict($"Slug '{request.Slug}' is already in use.");
        }

        var entity = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            LogoUrl = request.LogoUrl,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CommercialsEnabled = request.CommercialsEnabled,
            CommercialIntervalMinSeconds = request.CommercialIntervalMinSeconds,
            CommercialIntervalMaxSeconds = request.CommercialIntervalMaxSeconds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dataAccess.Insert(entity);
        return TypedResults.Created($"/api/channels/{entity.Id}", ToResponse(entity));
    }

    private static Results<Ok<ChannelResponse>, NotFound, BadRequest<string>, Conflict<string>> Update(
        Guid id,
        UpdateChannelRequest request,
        IDataAccess<ChannelEntity> dataAccess
    )
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
        {
            return TypedResults.BadRequest("Name and Slug are required.");
        }

        if (request.Slug != entity.Slug && dataAccess.List().Any(c => c.Slug == request.Slug))
        {
            return TypedResults.Conflict($"Slug '{request.Slug}' is already in use.");
        }

        entity.Name = request.Name;
        entity.Slug = request.Slug;
        entity.Description = request.Description;
        entity.LogoUrl = request.LogoUrl;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.CommercialsEnabled = request.CommercialsEnabled;
        entity.CommercialIntervalMinSeconds = request.CommercialIntervalMinSeconds;
        entity.CommercialIntervalMaxSeconds = request.CommercialIntervalMaxSeconds;
        entity.UpdatedAt = DateTime.UtcNow;

        dataAccess.Update(entity);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static Results<NoContent, NotFound> Delete(Guid id, IDataAccess<ChannelEntity> dataAccess)
    {
        var entity = dataAccess.GetByKey(id);
        if (entity is null) return TypedResults.NotFound();

        dataAccess.Delete(entity.Id);
        return TypedResults.NoContent();
    }

    private static ChannelResponse ToResponse(ChannelEntity e) =>
        new(
            e.Id, e.Name, e.Slug, e.Description, e.LogoUrl, e.DisplayOrder,
            e.IsActive, e.CommercialsEnabled, e.CommercialIntervalMinSeconds, e.CommercialIntervalMaxSeconds,
            e.CreatedAt, e.UpdatedAt
        );
}
