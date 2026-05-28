namespace SignalNine.Web.Data.Filesystem;

public record FsBrowseResponse(
    string Path,
    string? Parent,
    IReadOnlyList<FsEntryResponse> Entries
);
