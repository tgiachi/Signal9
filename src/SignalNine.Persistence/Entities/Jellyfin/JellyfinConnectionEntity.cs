using FreeSql.DataAnnotations;

namespace SignalNine.Persistence.Entities.Jellyfin;

[Table(Name = "jellyfin_connection")]
public class JellyfinConnectionEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(StringLength = 512)]
    public string BaseUrl { get; set; } = "";

    [Column(StringLength = 2048)]
    public string EncryptedApiKey { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime? LastVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
