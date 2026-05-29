namespace SignalNine.Web.Data.Streaming;

public sealed record ChannelStreamSnapshot(
    bool Running,
    Guid ChannelId,
    Guid? CurrentEntryId,
    string? CurrentEntryTitle,
    string? CurrentEntryKind,
    int? CurrentEntryPartIndex,
    int? CurrentEntryPartCount,
    int NextSegmentNumber,
    int? FfmpegPid,
    DateTime LastViewerAt,
    DateTime WillStopAt
);
