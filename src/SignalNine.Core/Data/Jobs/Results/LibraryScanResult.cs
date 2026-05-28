using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Data.Jobs.Results;

public sealed record ScannedItem(
    string Title,
    string SourceRef,
    int SourceType,
    int? DurationSeconds,
    int? MovieReleaseYear,
    string? MovieDirector,
    string? TvSeriesName,
    int? TvSeason,
    int? TvEpisode
);

public sealed record LibraryScanResult(
    Guid LibraryId,
    IReadOnlyList<ScannedItem> Items
) : IJobResult
{
    public string Type => "library.scan";
}
