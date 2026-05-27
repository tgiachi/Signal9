namespace SignalNine.Core.Data.Authentication;

public class JwtTokenResult
{
    public string AccessToken { get; set; } = "";

    public DateTime ExpiresAt { get; set; }
}
