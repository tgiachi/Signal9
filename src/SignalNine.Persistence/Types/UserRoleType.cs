namespace SignalNine.Persistence.Types;

/// <summary>
/// Defines the authorization role assigned to a user.
/// </summary>
public enum UserRoleType : byte
{
    /// <summary>
    /// Standard authenticated user.
    /// </summary>
    User = 0,

    /// <summary>
    /// Administrative user.
    /// </summary>
    Admin = 1
}
