namespace SignalNine.Persistence.Types;

/// <summary>
/// Discriminator for the kind of library item represented by a <c>ChannelMediaEntity</c>.
/// Drives Single Table Inheritance (STI) — type-specific columns on the entity are
/// significant only for the matching value here.
/// </summary>
public enum ChannelMediaType
{
    Commercial = 0,
    TvShow = 1,
    Bumper = 2,
    Movies = 3,
    Information = 4
}
