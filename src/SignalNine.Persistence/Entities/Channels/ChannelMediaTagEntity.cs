using FreeSql.DataAnnotations;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "channel_media_tags")]
[Index("{tablename}_idx_unique", nameof(ChannelMediaId) + "," + nameof(TagId), true)]
[Index("{tablename}_idx_tag", nameof(TagId), false)]
public class ChannelMediaTagEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChannelMediaId { get; set; }
    public Guid TagId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
