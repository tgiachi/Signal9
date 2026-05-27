namespace SignalNine.Persistence.Types;

/// <summary>
/// Identifies the backing source of a <c>ChannelMediaEntity</c>. Paired with
/// <c>SourceRef</c> (a free-form string interpreted according to this type)
/// the resolver layer can fetch the actual stream/file.
/// </summary>
public enum MediaSourceType
{
    Jellyfin = 0,
    LocalFile = 1,
    Url = 2
}
