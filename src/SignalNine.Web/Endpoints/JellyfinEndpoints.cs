using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jellyfin;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps Jellyfin connection management endpoints under <c>/api/jellyfin</c>.
/// </summary>
public static class JellyfinEndpoints
{
    public static WebApplication MapJellyfinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jellyfin").RequireAuthorization();

        group.MapGet("/connection", GetConnection);
        group.MapPut("/connection", SetConnection);
        group.MapDelete("/connection", ClearConnection);
        group.MapPost("/test", TestConnection);
        group.MapGet("/libraries", ListLibraries);

        return app;
    }

    private static async Task<Ok<JellyfinConnectionStatus>> GetConnection(
        IJellyfinConnectionService connection,
        CancellationToken ct
    )
    {
        var status = await connection.GetStatusAsync(ct);
        return TypedResults.Ok(status);
    }

    private static async Task<Results<NoContent, BadRequest<string>>> SetConnection(
        SetJellyfinConnectionRequest request,
        IJellyfinConnectionService connection,
        CancellationToken ct
    )
    {
        try
        {
            await connection.SetAsync(request.BaseUrl, request.ApiKey, ct);
            return TypedResults.NoContent();
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<NoContent> ClearConnection(
        IJellyfinConnectionService connection,
        CancellationToken ct
    )
    {
        await connection.ClearAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<JellyfinServerInfo>, UnauthorizedHttpResult, Conflict<string>, StatusCodeHttpResult>> TestConnection(
        IJellyfinService jellyfin,
        IJellyfinConnectionService connection,
        CancellationToken ct
    )
    {
        try
        {
            var info = await jellyfin.GetServerInfoAsync(ct);
            await connection.MarkVerifiedAsync(ct);
            return TypedResults.Ok(info);
        }
        catch (JellyfinNotConfiguredException)
        {
            return TypedResults.Conflict("Jellyfin connection is not configured.");
        }
        catch (JellyfinAuthException)
        {
            return TypedResults.Unauthorized();
        }
        catch (JellyfinUnreachableException)
        {
            return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<Results<Ok<IReadOnlyList<JellyfinLibrarySummary>>, UnauthorizedHttpResult, Conflict<string>, StatusCodeHttpResult>> ListLibraries(
        IJellyfinService jellyfin,
        CancellationToken ct
    )
    {
        try
        {
            var libs = await jellyfin.ListLibrariesAsync(ct);
            return TypedResults.Ok(libs);
        }
        catch (JellyfinNotConfiguredException)
        {
            return TypedResults.Conflict("Jellyfin connection is not configured.");
        }
        catch (JellyfinAuthException)
        {
            return TypedResults.Unauthorized();
        }
        catch (JellyfinUnreachableException)
        {
            return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
