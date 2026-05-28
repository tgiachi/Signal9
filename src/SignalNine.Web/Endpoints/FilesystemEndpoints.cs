// src/SignalNine.Web/Endpoints/FilesystemEndpoints.cs
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Web.Data.Filesystem;

namespace SignalNine.Web.Endpoints;

/// <summary>
/// Maps the server filesystem browse endpoint under <c>/api/fs</c>. Authenticated users only.
/// No path whitelist: the deployment perimeter (Docker mounts) defines the safe set.
/// </summary>
public static class FilesystemEndpoints
{
    public static WebApplication MapFilesystemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/fs").RequireAuthorization();

        group.MapGet("/browse", Browse);

        return app;
    }

    private static Results<Ok<FsBrowseResponse>, BadRequest<string>, NotFound, ForbidHttpResult, ProblemHttpResult> Browse(
        string? path
    )
    {
        var requested = string.IsNullOrWhiteSpace(path) ? "/" : path;

        if (!Path.IsPathRooted(requested))
        {
            return TypedResults.BadRequest("path must be absolute");
        }

        string canonical;
        try
        {
            canonical = Path.GetFullPath(requested);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"invalid path: {ex.Message}");
        }

        if (!Directory.Exists(canonical))
        {
            return TypedResults.NotFound();
        }

        List<FsEntryResponse> entries;
        try
        {
            entries = Directory
                .EnumerateFileSystemEntries(canonical)
                .Select(BuildEntry)
                .OrderBy(e => e.IsDirectory ? 0 : 1)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: 500);
        }

        var parent = Path.GetDirectoryName(canonical);
        if (string.IsNullOrEmpty(parent)) parent = null;

        return TypedResults.Ok(new FsBrowseResponse(canonical, parent, entries));
    }

    private static FsEntryResponse BuildEntry(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        bool isDirectory;
        try
        {
            var attrs = File.GetAttributes(fullPath);
            // Treat reparse points (symlinks) as non-directories so we never follow them.
            isDirectory = attrs.HasFlag(FileAttributes.Directory)
                          && !attrs.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            isDirectory = false;
        }
        return new FsEntryResponse(name, fullPath, isDirectory);
    }
}
