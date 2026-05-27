using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using SignalNine.Core.Data.Authentication;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class JwtTokenServiceTests
{
    private const int ExpirationMinutes = 30;
    private const int MinimumExpectedRemainingMinutes = 29;

    [Fact]
    public void CreateToken_ValidUser_ReturnsValidSignedToken()
    {
        var config = CreateConfig();
        var service = new JwtTokenService(config);
        var user = new JwtTokenUser
        {
            UserId = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@signalnine.local",
            Role = "Admin"
        };

        var token = service.CreateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.True(token.ExpiresAt > DateTime.UtcNow.AddMinutes(MinimumExpectedRemainingMinutes));

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var principal = handler.ValidateToken(
            token.AccessToken,
            JwtTokenService.CreateTokenValidationParameters(config.Jwt),
            out var validatedToken
        );

        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal(user.UserId.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(user.Username, principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value);
        Assert.Equal(user.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.True(principal.IsInRole(user.Role));
    }

    [Fact]
    public void CreateToken_ShortSecret_ThrowsInvalidOperationException()
    {
        var config = CreateConfig();
        config.Jwt.Secret = "short-secret";
        var service = new JwtTokenService(config);
        var user = new JwtTokenUser
        {
            UserId = Guid.NewGuid(),
            Username = "admin",
            Role = "Admin"
        };

        Assert.Throws<InvalidOperationException>(() => service.CreateToken(user));
    }

    private static SignalNineConfig CreateConfig()
        => new()
        {
            Jwt =
            {
                Issuer = "SignalNine.Tests",
                Audience = "SignalNine.Tests.Client",
                Secret = "signalnine-test-secret-with-enough-length",
                ExpirationMinutes = ExpirationMinutes
            }
        };
}
