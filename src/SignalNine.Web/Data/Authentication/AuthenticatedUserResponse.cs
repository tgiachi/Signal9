namespace SignalNine.Web.Data.Authentication;

/// <summary>
/// Represents the authenticated user returned by the login endpoint.
/// </summary>
public sealed record AuthenticatedUserResponse
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    public string Username { get; init; } = "";

    /// <summary>
    /// Gets the user email.
    /// </summary>
    public string Email { get; init; } = "";

    /// <summary>
    /// Gets the user role.
    /// </summary>
    public string Role { get; init; } = "";
}
