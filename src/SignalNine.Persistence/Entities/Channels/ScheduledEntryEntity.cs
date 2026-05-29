using FreeSql.DataAnnotations;
using SignalNine.Persistence.Types;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "scheduled_entries")]
[Index("{tablename}_idx_channel_start", nameof(ChannelId) + "," + nameof(StartAt), false)]
[Index("{tablename}_idx_block", nameof(SourceBlockId), false)]
public class ScheduledEntryEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChannelId { get; set; }

    public Guid? SourceBlockId { get; set; }

    public DateTime StartAt { get; set; }

    public int DurationSeconds { get; set; }

    public ScheduledEntryKind Kind { get; set; }

    public Guid ChannelMediaId { get; set; }

    public int MediaPartIndex { get; set; }

    public int MediaPartCount { get; set; }

    public int MediaOffsetSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
