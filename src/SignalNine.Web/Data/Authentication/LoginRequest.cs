namespace SignalNine.Web.Data.Authentication;

/// <summary>
/// Payload used to authenticate a user.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>
    /// Gets the username or email used for authentication.
    /// </summary>
    public required string Username { get; init; } = "";

    /// <summary>
    /// Gets the plain-text password supplied by the client.
    /// </summary>
    public required string Password { get; init; } = "";
}
