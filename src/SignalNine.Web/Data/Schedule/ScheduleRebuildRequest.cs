namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduleRebuildRequest(DateTime? FromUtc, int? HoursAhead);
