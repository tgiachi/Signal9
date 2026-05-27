namespace SignalNine.Core.Data.Config;

public class JwtConfig
{
    private const int DefaultExpirationMinutes = 60;

    public string Issuer { get; set; } = "SignalNine";

    public string Audience { get; set; } = "SignalNine";

    public string Secret { get; set; } = "signalnine-development-secret-change-before-production";

    public int ExpirationMinutes { get; set; } = DefaultExpirationMinutes;
}
