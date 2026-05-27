namespace SignalNine.Core.Data.Jellyfin;

public record JellyfinItem(
    string Id,
    string Name,
    string Type,
    long? RunTimeTicks,
    int? ProductionYear,
    string? Overview,
    string? SeriesName,
    int? ParentIndexNumber,
    int? IndexNumber
);
