namespace SignalNine.Core.Data.Jellyfin;

public class JellyfinUnreachableException : Exception
{
    public int? StatusCode { get; }

    public JellyfinUnreachableException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}
