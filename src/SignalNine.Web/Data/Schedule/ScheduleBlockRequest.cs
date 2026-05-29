using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduleBlockRequest(
    string Name,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    int DurationMinutes,
    ScheduleBlockRuleType RuleType,
    Guid? PinnedChannelMediaId,
    string? SeriesName,
    string? TagFilterCsv,
    string? TypeFilterCsv,
    bool IsActive
);
