using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps FFmpeg pool inspection endpoints under <c>/api/ffmpeg/processes</c>.
/// </summary>
public static class FfmpegPoolEndpoints
{
    public static WebApplication MapFfmpegPoolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ffmpeg/processes").RequireAuthorization();

        group.MapGet(string.Empty, List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/{id:guid}/cancel", Cancel);

        return app;
    }

    private static Ok<IReadOnlyList<FfmpegProcessSnapshot>> List(IFfmpegPool pool)
    {
        return TypedResults.Ok(pool.List());
    }

    private static Results<Ok<FfmpegProcessSnapshot>, NotFound> GetById(Guid id, IFfmpegPool pool)
    {
        var snapshot = pool.Get(id);
        return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(snapshot);
    }

    private static async Task<Results<NoContent, NotFound, Conflict<string>>> Cancel(
        Guid id,
        IFfmpegPool pool,
        CancellationToken ct
    )
    {
        var canceled = await pool.CancelAsync(id, ct);
        if (canceled) return TypedResults.NoContent();

        var snapshot = pool.Get(id);
        if (snapshot is null) return TypedResults.NotFound();

        return TypedResults.Conflict($"Process is already in terminal state '{snapshot.Status}'.");
    }
}
