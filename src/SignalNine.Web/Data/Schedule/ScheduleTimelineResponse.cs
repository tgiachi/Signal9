namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduleTimelineResponse(
    Guid ChannelId,
    DateTime From,
    DateTime To,
    IReadOnlyList<ScheduledEntryResponse> Entries
);
