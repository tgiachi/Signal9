using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Services.Streaming;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Streaming;
using SignalNine.Web.Services.Streaming;

namespace SignalNine.Web.Endpoints;

public static class StreamEndpoints
{
    public static WebApplication MapStreamEndpoints(this WebApplication app)
    {
        app.MapPut("/api/channels/{channelId:guid}/effects", PutEffects).RequireAuthorization();
        app.MapGet("/api/streaming/effects/catalog", GetCatalog).RequireAuthorization();
        app.MapGet("/api/channels/{channelId:guid}/stream/status", GetStatus).RequireAuthorization();
        app.MapDelete("/api/channels/{channelId:guid}/stream", DeleteStream).RequireAuthorization();
        app.MapGet("/api/channels/{channelId:guid}/stream/index.m3u8", GetPlaylist).RequireAuthorization();
        app.MapGet("/api/channels/{channelId:guid}/stream/{file}", GetSegment).RequireAuthorization();
        return app;
    }

    private static async Task<Results<NoContent, NotFound>> PutEffects(
        Guid channelId,
        ChannelEffectsRequest body,
        IDataAccess<ChannelEntity> channels,
        ChannelStreamCoordinator coordinator
    )
    {
        var channel = channels.GetByKey(channelId);
        if (channel is null) return TypedResults.NotFound();

        channel.VideoEffectsJson = JsonSerializer.Serialize(body.Effects);
        channel.UpdatedAt = DateTime.UtcNow;
        channels.Update(channel);

        await coordinator.StopAsync(channelId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static Ok<IReadOnlyList<EffectCatalogItemResponse>> GetCatalog()
    {
        var items = EffectCatalog.Items
            .Select(d => new EffectCatalogItemResponse(d.Kind, d.Label, d.Description, d.Parameters))
            .ToList();
        return TypedResults.Ok<IReadOnlyList<EffectCatalogItemResponse>>(items);
    }

    private static IResult GetStatus(Guid channelId, ChannelStreamCoordinator coordinator)
    {
        var snapshot = coordinator.GetSnapshot(channelId);
        if (snapshot is null)
        {
            return Results.Content("null", "application/json");
        }
        return Results.Json(snapshot);
    }

    private static async Task<NoContent> DeleteStream(Guid channelId, ChannelStreamCoordinator coordinator)
    {
        await coordinator.StopAsync(channelId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetPlaylist(
        Guid channelId,
        IDataAccess<ChannelEntity> channels,
        ChannelStreamCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (channels.GetByKey(channelId) is null) return TypedResults.NotFound();
        var director = coordinator.GetOrStart(channelId);
        director.Touch();

        var path = Path.Combine(coordinator.OutputDir(channelId), "index.m3u8");
        var waited = TimeSpan.Zero;
        var step = TimeSpan.FromMilliseconds(200);
        while (!File.Exists(path) && waited < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(step, cancellationToken).ConfigureAwait(false);
            waited += step;
        }
        if (!File.Exists(path)) return Results.StatusCode(503);
        return Results.File(path, "application/vnd.apple.mpegurl");
    }

    private static IResult GetSegment(Guid channelId, string file, ChannelStreamCoordinator coordinator)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(file, @"^seg\d+\.ts$"))
        {
            return TypedResults.NotFound();
        }
        coordinator.Touch(channelId);
        var path = Path.Combine(coordinator.OutputDir(channelId), file);
        if (!File.Exists(path)) return TypedResults.NotFound();
        return Results.File(path, "video/mp2t", enableRangeProcessing: true);
    }
}
