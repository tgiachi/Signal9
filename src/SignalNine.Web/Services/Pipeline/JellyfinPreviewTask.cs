using System.Globalization;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Types;
using SignalNine.Web.Data.Pipeline;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class JellyfinPreviewTask : IPipelineTask
{
    private const int TaskOrder = 150;
    private const string PreviewsRootName = "previews";
    private const string ExistingThumbnailPattern = "thumb-*.jpg";

    private readonly IJellyfinService _jellyfin;
    private readonly DirectoriesConfig _directories;
    private readonly PipelineConfig _config;

    public string Name => "jellyfin-preview";

    public int Order => TaskOrder;

    public bool IsEnabled => _config.Tasks.JellyfinPreview.Enabled;

    public JellyfinPreviewTask(
        IJellyfinService jellyfin,
        DirectoriesConfig directories,
        PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(jellyfin);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(config);

        _jellyfin = jellyfin;
        _directories = directories;
        _config = config;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Media.SourceType != MediaSourceType.Jellyfin ||
            string.IsNullOrWhiteSpace(context.Media.SourceRef))
        {
            return;
        }

        var outputDir = Path.Combine(
            _directories[DirectoryType.Assets],
            PreviewsRootName,
            context.Media.Id.ToString()
        );
        if (!_config.Tasks.JellyfinPreview.OverwriteExisting && HasExistingPreview(outputDir))
        {
            return;
        }

        var images = await _jellyfin.GetPreviewImagesAsync(
            context.Media.SourceRef,
            _config.Tasks.JellyfinPreview.MaxImages,
            ct
        ).ConfigureAwait(false);
        if (images.Count == 0)
        {
            return;
        }

        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
        Directory.CreateDirectory(outputDir);

        for (var i = 0; i < images.Count; i++)
        {
            var index = (i + 1).ToString("000", CultureInfo.InvariantCulture);
            var fileName = $"thumb-{index}.jpg";
            var path = Path.Combine(outputDir, fileName);
            await File.WriteAllBytesAsync(path, images[i].Content, ct).ConfigureAwait(false);
        }
    }

    private static bool HasExistingPreview(string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return false;
        }

        return Directory.EnumerateFiles(outputDir, ExistingThumbnailPattern).Any();
    }
}
