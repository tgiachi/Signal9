using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Config;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps TOML configuration HTTP endpoints under <c>/api/config</c>.
/// </summary>
public static class ConfigEndpoints
{
    /// <summary>
    /// Maps the configuration endpoints on the supplied application.
    /// </summary>
    public static WebApplication MapConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/config").AllowAnonymous();

        group.MapGet(
            string.Empty,
            GetConfigAsync
        );

        group.MapPost(
            string.Empty,
            SaveConfigAsync
        );

        return app;
    }

    private static async Task<Results<ContentHttpResult, NotFound>> GetConfigAsync(
        IConfigService configService,
        CancellationToken cancellationToken
    )
    {
        var path = configService.ConfigPath;

        if (!File.Exists(path))
        {
            await configService.LoadAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(path))
        {
            return TypedResults.NotFound();
        }

        var toml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return TypedResults.Text(toml, "text/plain");
    }

    private static async Task<Results<Ok, UnprocessableEntity<ConfigValidationError>, BadRequest<string>>> SaveConfigAsync(
        HttpRequest request,
        IConfigService configService,
        CancellationToken cancellationToken
    )
    {
        string body;

        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return TypedResults.BadRequest("Request body must contain TOML text.");
        }

        var validation = configService.Validate(body);

        if (!validation.IsValid)
        {
            return TypedResults.UnprocessableEntity(
                new ConfigValidationError
                {
                    Message = validation.Message ?? "Invalid TOML.",
                    Line = validation.Line,
                    Column = validation.Column
                }
            );
        }

        await configService.SaveRawAsync(body, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok();
    }
}
