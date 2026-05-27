using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Channels;

public record CreateChannelMediaRequest(
    ChannelMediaType Type,
    string Title,
    int? DurationSeconds,
    MediaSourceType SourceType,
    string? SourceRef,
    int? MovieReleaseYear,
    string? MovieDirector,
    string? TvSeriesName,
    int? TvSeason,
    int? TvEpisode,
    string? CommercialAdvertiser,
    string? CommercialCampaign,
    string? InformationEdition
);
