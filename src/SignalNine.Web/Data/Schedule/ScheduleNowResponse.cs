namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduleNowResponse(
    ScheduledEntryResponse? Current,
    ScheduledEntryResponse? Next,
    int SecondsIntoCurrent,
    DateTime ComputedAt
);
