namespace SignalNine.Core.Data.Authentication;

public class JwtTokenUser
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = "";

    public string Email { get; set; } = "";

    public string Role { get; set; } = "";
}
