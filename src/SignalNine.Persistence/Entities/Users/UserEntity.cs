using FreeSql.DataAnnotations;
using SignalNine.Persistence.Types;

namespace SignalNine.Persistence.Entities.Users;

[Table(Name = "users"),Index("{tablename}_idx_username", nameof(Username), true),Index("{tablename}_idx_email", nameof(Email), true)]
public class UserEntity
{
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(StringLength = 128)]
    public string Username { get; set; } = "";

    [Column(StringLength = 256)]
    public string Email { get; set; } = "";

    [Column(StringLength = 512)]
    public string PasswordHash { get; set; } = "";

    public UserRoleType Role { get; set; } = UserRoleType.User;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}
