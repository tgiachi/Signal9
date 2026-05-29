using FreeSql.DataAnnotations;
using SignalNine.Persistence.Types;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "schedule_blocks")]
[Index("{tablename}_idx_channel_day", nameof(ChannelId) + "," + nameof(DayOfWeek), false)]
[Index("{tablename}_idx_active", nameof(IsActive), false)]
public class ScheduleBlockEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChannelId { get; set; }

    [Column(StringLength = 128)]
    public string Name { get; set; } = "";

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public int DurationMinutes { get; set; }

    public ScheduleBlockRuleType RuleType { get; set; }

    public Guid? PinnedChannelMediaId { get; set; }

    [Column(StringLength = 256)]
    public string? SeriesName { get; set; }

    public Guid? SeriesCursorChannelMediaId { get; set; }

    [Column(StringLength = 512)]
    public string? TagFilterCsv { get; set; }

    [Column(StringLength = 128)]
    public string? TypeFilterCsv { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
