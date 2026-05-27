using SignalNine.Core.Data.Channels;

namespace SignalNine.Core.Interfaces;

public interface ILocalLibraryWalker
{
    /// <summary>Enumerates media files under rootPath, yielding each as a LocalLibraryItem.</summary>
    /// <exception cref="DirectoryNotFoundException">When rootPath does not exist or is not a directory.</exception>
    IEnumerable<LocalLibraryItem> Walk(string rootPath, CancellationToken ct);
}
