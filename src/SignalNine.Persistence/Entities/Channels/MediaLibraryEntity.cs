using FreeSql.DataAnnotations;
using SignalNine.Persistence.Types;

namespace SignalNine.Persistence.Entities.Channels;

[Table(Name = "media_libraries")]
[Index("{tablename}_idx_source", nameof(SourceType) + "," + nameof(SourceRef), true)]
[Index("{tablename}_idx_active", nameof(IsActive), false)]
public class MediaLibraryEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(StringLength = 128)]
    public string Name { get; set; } = "";

    [Column(StringLength = 512)]
    public string? Description { get; set; }

    public ChannelMediaType DefaultMediaType { get; set; }

    public MediaSourceType SourceType { get; set; } = MediaSourceType.Jellyfin;

    [Column(StringLength = 512)]
    public string SourceRef { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime? LastScannedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
