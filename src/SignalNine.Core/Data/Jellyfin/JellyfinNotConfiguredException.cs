namespace SignalNine.Core.Data.Jellyfin;

public class JellyfinNotConfiguredException : Exception
{
    public JellyfinNotConfiguredException() : base("Jellyfin connection is not configured.") { }
}
