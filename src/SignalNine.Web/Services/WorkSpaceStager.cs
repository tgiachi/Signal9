using SignalNine.Core.Data.Config;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;

namespace SignalNine.Web.Services;

public sealed class WorkSpaceStager
{
    private const string InputDirName = "input";
    private const string JobsDirName = "jobs";

    private readonly WorkSpaceConfig _workspace;

    public WorkSpaceStager(SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _workspace = config.WorkSpace;
    }

    /// <summary>
    /// Copies the source file for <paramref name="media"/> into a fresh per-job scratch dir.
    /// Returns the absolute work dir and the relative input file (e.g. "input/movie.mp4")
    /// that the worker should join with WorkDir to find the input.
    /// </summary>
    public Task<(string WorkDir, string RelativeInputFile)> StageAsync(
        ChannelMediaEntity media,
        MediaLibraryEntity library,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(library);

        var jobScratchId = Guid.NewGuid();
        var workDir = Path.Combine(_workspace.Path, JobsDirName, jobScratchId.ToString());
        var inputDir = Path.Combine(workDir, InputDirName);
        Directory.CreateDirectory(inputDir);

        if (media.SourceType == MediaSourceType.LocalFile)
        {
            var sourceRoot = library.SourceRef ?? string.Empty;
            var sourceRel = media.SourceRef ?? string.Empty;
            var src = Path.Combine(sourceRoot, sourceRel);
            if (!File.Exists(src))
            {
                throw new FileNotFoundException(
                    $"Source file not found for ChannelMedia {media.Id}: '{src}'", src
                );
            }
            var filename = Path.GetFileName(src);
            var destFile = Path.Combine(inputDir, filename);
            File.Copy(src, destFile, overwrite: true);
            return Task.FromResult((workDir, Path.Combine(InputDirName, filename).Replace('\\', '/')));
        }

        // Jellyfin/Url: source streams remotely — out of Phase 4 scope to pre-stage.
        // Return workDir with empty relative input; caller can decide how to handle.
        return Task.FromResult((workDir, string.Empty));
    }
}
