namespace SignalNine.Core.Data.Jellyfin;

public class JellyfinAuthException : Exception
{
    public JellyfinAuthException(string message) : base(message) { }
}
