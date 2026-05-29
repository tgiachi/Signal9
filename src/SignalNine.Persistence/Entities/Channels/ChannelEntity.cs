using FreeSql.DataAnnotations;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "channels")]
[Index("{tablename}_idx_slug", nameof(Slug), true)]
[Index("{tablename}_idx_active", nameof(IsActive), false)]
public class ChannelEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(StringLength = 128)]
    public string Name { get; set; } = "";

    [Column(StringLength = 64)]
    public string Slug { get; set; } = "";

    [Column(StringLength = 512)]
    public string? Description { get; set; }

    [Column(StringLength = 1024)]
    public string? LogoUrl { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public bool CommercialsEnabled { get; set; } = true;
    public int CommercialIntervalMinSeconds { get; set; } = 120;
    public int CommercialIntervalMaxSeconds { get; set; } = 300;

    public int CommercialBreakSize { get; set; } = 2;

    public int CommercialIntervalJitterSeconds { get; set; } = 120;

    public bool CommercialBumpersEnabled { get; set; } = true;

    [Column(StringLength = 512)]
    public string? FallbackTagFilterCsv { get; set; }

    [Column(StringLength = 128)]
    public string? FallbackTypeFilterCsv { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
