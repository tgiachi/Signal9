using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduledEntryResponse(
    Guid Id,
    Guid? SourceBlockId,
    DateTime StartAt,
    int DurationSeconds,
    ScheduledEntryKind Kind,
    Guid ChannelMediaId,
    string Title,
    int PartIndex,
    int PartCount,
    int MediaOffsetSeconds
);
