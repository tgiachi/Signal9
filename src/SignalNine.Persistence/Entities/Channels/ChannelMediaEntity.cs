using FreeSql.DataAnnotations;
using SignalNine.Persistence.Types;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "channel_media")]
[Index("{tablename}_idx_type", nameof(Type), false)]
[Index("{tablename}_idx_active", nameof(IsActive), false)]
[Index("{tablename}_idx_source", nameof(SourceType) + "," + nameof(SourceRef), false)]
public class ChannelMediaEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public ChannelMediaType Type { get; set; }

    [Column(StringLength = 256)]
    public string Title { get; set; } = "";

    public int? DurationSeconds { get; set; }

    public bool IsActive { get; set; } = true;

    public MediaSourceType SourceType { get; set; } = MediaSourceType.Jellyfin;

    [Column(StringLength = 512)]
    public string? SourceRef { get; set; }

    // Movies
    public int? MovieReleaseYear { get; set; }
    [Column(StringLength = 128)] public string? MovieDirector { get; set; }

    // TvShow
    [Column(StringLength = 256)] public string? TvSeriesName { get; set; }
    public int? TvSeason { get; set; }
    public int? TvEpisode { get; set; }

    // Commercial
    [Column(StringLength = 128)] public string? CommercialAdvertiser { get; set; }
    [Column(StringLength = 128)] public string? CommercialCampaign { get; set; }

    // Information
    [Column(StringLength = 64)] public string? InformationEdition { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
