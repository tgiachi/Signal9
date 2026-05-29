using SignalNine.Persistence.Types;

namespace SignalNine.Web.Data.Schedule;

public sealed record ScheduleBlockResponse(
    Guid Id,
    Guid ChannelId,
    string Name,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    int DurationMinutes,
    ScheduleBlockRuleType RuleType,
    Guid? PinnedChannelMediaId,
    string? SeriesName,
    Guid? SeriesCursorChannelMediaId,
    string? TagFilterCsv,
    string? TypeFilterCsv,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
