namespace SignalNine.Web.Data.Authentication;

/// <summary>
/// Represents a successful authentication response.
/// </summary>
public sealed record LoginResponse
{
    /// <summary>
    /// Gets the JWT access token.
    /// </summary>
    public string AccessToken { get; init; } = "";

    /// <summary>
    /// Gets the token type.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Gets the access token expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the authenticated user.
    /// </summary>
    public required AuthenticatedUserResponse User { get; init; }
}
