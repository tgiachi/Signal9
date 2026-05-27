using SignalNine.Core.Data.Authentication;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Creates JWT access tokens for authenticated SignalNine users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT access token for a user.
    /// </summary>
    /// <param name="user">The user data to include in the token claims.</param>
    /// <returns>The access token and its expiration timestamp.</returns>
    JwtTokenResult CreateToken(JwtTokenUser user);
}
