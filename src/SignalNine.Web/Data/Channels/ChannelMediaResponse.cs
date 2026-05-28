using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Channels;

public record ChannelMediaResponse(
    Guid Id,
    ChannelMediaType Type,
    string Title,
    int? DurationSeconds,
    bool IsActive,
    MediaSourceType SourceType,
    string? SourceRef,
    int? MovieReleaseYear,
    string? MovieDirector,
    string? TvSeriesName,
    int? TvSeason,
    int? TvEpisode,
    string? CommercialAdvertiser,
    string? CommercialCampaign,
    string? InformationEdition,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TagSummary> Tags
);
