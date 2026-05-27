using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;
using SignalNine.Core.Data.Authentication;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public class JwtTokenService : IJwtTokenService
{
    private const int ClockSkewMinutes = 1;
    private const int MinimumSecretByteLength = 32;

    private readonly JwtConfig _jwtConfig;

    public JwtTokenService(SignalNineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _jwtConfig = config.Jwt;
    }

    public JwtTokenResult CreateToken(JwtTokenUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateConfig(_jwtConfig);
        ValidateUser(user);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_jwtConfig.ExpirationMinutes);
        var signingCredentials = new SigningCredentials(
            CreateSecurityKey(_jwtConfig),
            SecurityAlgorithms.HmacSha256
        );
        var claims = CreateClaims(user);
        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            now,
            expiresAt,
            signingCredentials
        );

        return new JwtTokenResult
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt
        };
    }

    public static TokenValidationParameters CreateTokenValidationParameters(JwtConfig jwtConfig)
    {
        ArgumentNullException.ThrowIfNull(jwtConfig);
        ValidateConfig(jwtConfig);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSecurityKey(jwtConfig),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(ClockSkewMinutes),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = ClaimTypes.Role
        };
    }

    private static IReadOnlyList<Claim> CreateClaims(JwtTokenUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        return claims;
    }

    private static SymmetricSecurityKey CreateSecurityKey(JwtConfig jwtConfig)
        => new(Encoding.UTF8.GetBytes(jwtConfig.Secret));

    private static void ValidateConfig(JwtConfig jwtConfig)
    {
        if (string.IsNullOrWhiteSpace(jwtConfig.Issuer))
        {
            throw new InvalidOperationException("JWT issuer cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(jwtConfig.Audience))
        {
            throw new InvalidOperationException("JWT audience cannot be empty.");
        }

        if (Encoding.UTF8.GetByteCount(jwtConfig.Secret) < MinimumSecretByteLength)
        {
            throw new InvalidOperationException("JWT secret must be at least 32 bytes.");
        }

        if (jwtConfig.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("JWT expiration must be greater than zero minutes.");
        }
    }

    private static void ValidateUser(JwtTokenUser user)
    {
        if (user.UserId == Guid.Empty)
        {
            throw new ArgumentException("JWT user id cannot be empty.", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            throw new ArgumentException("JWT username cannot be empty.", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            throw new ArgumentException("JWT role cannot be empty.", nameof(user));
        }
    }
}
