using SignalNine.Core.Data.Channels;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public class LocalLibraryWalker : ILocalLibraryWalker
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".webm", ".ts", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg"
    };

    public IEnumerable<LocalLibraryItem> Walk(string rootPath, CancellationToken ct)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Library root does not exist: {rootPath}");
        }

        foreach (var path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (!AllowedExtensions.Contains(Path.GetExtension(path))) continue;

            var info = new FileInfo(path);
            var rel = Path.GetRelativePath(rootPath, path).Replace(Path.DirectorySeparatorChar, '/');
            var title = Path.GetFileNameWithoutExtension(path);
            yield return new LocalLibraryItem(rel, title, info.Length);
        }
    }
}
