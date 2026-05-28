using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Workers;

namespace SignalNine.Web.Endpoints;

public static class WorkerEndpoints
{
    public static WebApplication MapWorkerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workers").RequireAuthorization();

        group.MapGet(string.Empty, ListWorkers);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<WorkerResponse>>> ListWorkers(
        IWorkerRegistry registry,
        CancellationToken cancellationToken
    )
    {
        var infos = await registry.ListAsync(cancellationToken).ConfigureAwait(false);
        var responses = infos.Select(WorkerResponse.From).ToList();
        return TypedResults.Ok<IReadOnlyList<WorkerResponse>>(responses);
    }
}
