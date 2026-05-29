namespace SignalNine.Web.Data.Schedule;

public sealed record SchedulePlanJobPayload(Guid ChannelId, DateTime FromUtc, int HoursAhead);
