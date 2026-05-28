using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Web.Data.Pipeline;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class JellyfinTagsTask : IPipelineTask
{
    private const int TaskOrder = 75;
    private const int MaxTagNameLength = 64;
    private const int MaxTagLabelLength = 128;

    private readonly IJellyfinService _jellyfin;
    private readonly IDataAccess<TagEntity> _tags;
    private readonly IDataAccess<ChannelMediaTagEntity> _mediaTags;
    private readonly PipelineConfig _config;

    public string Name => "jellyfin-tags";

    public int Order => TaskOrder;

    public bool IsEnabled => _config.Tasks.JellyfinTags.Enabled;

    public JellyfinTagsTask(
        IJellyfinService jellyfin,
        IDataAccess<TagEntity> tags,
        IDataAccess<ChannelMediaTagEntity> mediaTags,
        PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(jellyfin);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(mediaTags);
        ArgumentNullException.ThrowIfNull(config);

        _jellyfin = jellyfin;
        _tags = tags;
        _mediaTags = mediaTags;
        _config = config;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Media.SourceType != MediaSourceType.Jellyfin ||
            string.IsNullOrWhiteSpace(context.Media.SourceRef) ||
            !SupportsMetadataTags(context.Media.Type))
        {
            return;
        }

        var itemTags = await _jellyfin.GetItemTagsAsync(context.Media.SourceRef, ct).ConfigureAwait(false);
        var labels = NormalizeLabels(itemTags.Genres.Concat(itemTags.Tags));
        if (labels.Count == 0)
        {
            return;
        }

        var existingTags = _tags.List().ToList();
        var existingJoins = _mediaTags
            .List()
            .Where(join => join.ChannelMediaId == context.Media.Id)
            .ToList();

        foreach (var label in labels)
        {
            ct.ThrowIfCancellationRequested();
            var tag = EnsureTag(label, existingTags);
            EnsureMediaTag(context.Media.Id, tag.Id, existingJoins);
        }
    }

    private static bool SupportsMetadataTags(ChannelMediaType mediaType)
        => mediaType is ChannelMediaType.Movies or ChannelMediaType.TvShow;

    private static IReadOnlyList<string> NormalizeLabels(IEnumerable<string> labels)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var normalized = NormalizeLabel(label);
            if (seen.Add(normalized))
            {
                results.Add(normalized);
            }
        }

        return results;
    }

    private TagEntity EnsureTag(string label, List<TagEntity> existingTags)
    {
        var normalizedName = NormalizeTagName(label);
        var tag = existingTags.FirstOrDefault(
            existing => existing.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)
        );
        if (tag is not null)
        {
            return tag;
        }

        tag = new TagEntity
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Label = TrimTo(label, MaxTagLabelLength),
            CreatedAt = DateTime.UtcNow
        };

        _tags.Insert(tag);
        existingTags.Add(tag);

        return tag;
    }

    private void EnsureMediaTag(
        Guid mediaId,
        Guid tagId,
        List<ChannelMediaTagEntity> existingJoins
    )
    {
        if (existingJoins.Any(join => join.TagId == tagId))
        {
            return;
        }

        var join = new ChannelMediaTagEntity
        {
            Id = Guid.NewGuid(),
            ChannelMediaId = mediaId,
            TagId = tagId,
            CreatedAt = DateTime.UtcNow
        };

        _mediaTags.Insert(join);
        existingJoins.Add(join);
    }

    private static string NormalizeLabel(string value)
        => value.Trim();

    private static string NormalizeTagName(string value)
        => TrimTo(value.Trim().ToLowerInvariant(), MaxTagNameLength);

    private static string TrimTo(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
