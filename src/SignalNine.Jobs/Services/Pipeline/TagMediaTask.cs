using SignalNine.Core.Data.Config;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Jobs.Data.Pipeline;
using SignalNine.Jobs.Interfaces;

namespace SignalNine.Jobs.Services.Pipeline;

public class TagMediaTask : IPipelineTask
{
    private const int TaskOrder = 50;

    private static readonly IReadOnlyDictionary<ChannelMediaType, (string Name, string Label)[]> TagsByType =
        new Dictionary<ChannelMediaType, (string Name, string Label)[]>
        {
            [ChannelMediaType.Commercial] =
            [
                ("commercials", "Commercials"),
                ("adv", "Advertising")
            ],
            [ChannelMediaType.TvShow] =
            [
                ("tv-shows", "TV Shows")
            ],
            [ChannelMediaType.Bumper] =
            [
                ("bumpers", "Bumpers")
            ],
            [ChannelMediaType.Movies] =
            [
                ("movies", "Movies")
            ],
            [ChannelMediaType.Information] =
            [
                ("information", "Information")
            ]
        };

    private readonly IDataAccess<TagEntity> _tags;
    private readonly IDataAccess<ChannelMediaTagEntity> _mediaTags;
    private readonly PipelineConfig _config;

    public string Name => "tagger";

    public int Order => TaskOrder;

    public bool IsEnabled => _config.Tasks.Tagger.Enabled;

    public TagMediaTask(
        IDataAccess<TagEntity> tags,
        IDataAccess<ChannelMediaTagEntity> mediaTags,
        PipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(mediaTags);
        ArgumentNullException.ThrowIfNull(config);

        _tags = tags;
        _mediaTags = mediaTags;
        _config = config;
    }

    public Task ExecuteAsync(PipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TagsByType.TryGetValue(context.Media.Type, out var tagDefinitions))
        {
            return Task.CompletedTask;
        }

        var existingTags = _tags.List().ToList();
        var existingJoins = _mediaTags
            .List()
            .Where(join => join.ChannelMediaId == context.Media.Id)
            .ToList();

        foreach (var tagDefinition in tagDefinitions)
        {
            ct.ThrowIfCancellationRequested();

            var tag = EnsureTag(tagDefinition.Name, tagDefinition.Label, existingTags);
            EnsureMediaTag(context.Media.Id, tag.Id, existingJoins);
        }

        return Task.CompletedTask;
    }

    private TagEntity EnsureTag(string name, string label, List<TagEntity> existingTags)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
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
            Label = label,
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
}
