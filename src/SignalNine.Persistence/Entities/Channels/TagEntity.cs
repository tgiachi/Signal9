using FreeSql.DataAnnotations;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "tags")]
[Index("{tablename}_idx_name", nameof(Name), true)]
public class TagEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(StringLength = 64)]
    public string Name { get; set; } = "";

    [Column(StringLength = 128)]
    public string? Label { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
