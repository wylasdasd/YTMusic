namespace YTMusic.BLL.Models;

/// <summary>
/// 按本地文件所在目录把曲目分成可展开的组。直接放在库根目录下的文件单独成一组。
/// </summary>
public static class LocalDirectoryTree
{
    public const string RootGroupName = "根目录";

    public static IReadOnlyList<LocalDirectoryNode<FavoriteTrack>> Build(
        IEnumerable<FavoriteTrack> tracks,
        string? libraryRoot,
        bool sortByName)
        => Build(tracks, track => track.LocalFilePath, track => track.Title, track => track.AddedDate, libraryRoot, sortByName);

    public static IReadOnlyList<LocalDirectoryNode<T>> Build<T>(
        IEnumerable<T> tracks,
        Func<T, string?> filePath,
        Func<T, string?> title,
        Func<T, DateTime> addedDate,
        string? libraryRoot,
        bool sortByName)
    {
        var root = new BuilderNode<T>(string.Empty, string.Empty);
        foreach (var track in tracks)
        {
            var path = filePath(track);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var relative = ToRelativeDirectory(directory, libraryRoot);
            var current = root;
            if (!string.IsNullOrEmpty(relative))
            {
                var segments = relative.Split(
                    new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
                var builtPath = string.Empty;
                foreach (var segment in segments)
                {
                    builtPath = builtPath.Length == 0 ? segment : Path.Combine(builtPath, segment);
                    current = current.GetOrAdd(segment, builtPath);
                }
            }

            current.Tracks.Add(track);
        }

        var nodes = new List<LocalDirectoryNode<T>>();
        if (root.Tracks.Count > 0)
        {
            nodes.Add(Freeze(root, RootDisplayName(libraryRoot), string.Empty, title, addedDate, sortByName, includeChildren: false));
        }

        nodes.AddRange(root.Children.Values
            .OrderBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(child => Freeze(child, child.Name, child.RelativePath, title, addedDate, sortByName, includeChildren: true)));
        return nodes;
    }

    public static List<T> Flatten<T>(IReadOnlyList<LocalDirectoryNode<T>> nodes)
    {
        var list = new List<T>();
        Append(nodes, list);
        return list;
    }

    private static void Append<T>(IReadOnlyList<LocalDirectoryNode<T>> nodes, List<T> list)
    {
        foreach (var node in nodes)
        {
            Append(node.Children, list);
            list.AddRange(node.Tracks);
        }
    }

    private static string RootDisplayName(string? libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            return RootGroupName;
        }

        var name = new DirectoryInfo(libraryRoot).Name;
        return string.IsNullOrWhiteSpace(name) ? RootGroupName : name;
    }

    private static string ToRelativeDirectory(string directory, string? libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(libraryRoot))
        {
            var relative = Path.GetRelativePath(libraryRoot, directory);
            if (relative == ".")
            {
                return string.Empty;
            }

            var escapesRoot = relative == ".."
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relative);
            if (!escapesRoot)
            {
                return relative;
            }
        }

        return new DirectoryInfo(directory).Name;
    }

    private static LocalDirectoryNode<T> Freeze<T>(
        BuilderNode<T> node,
        string name,
        string relativePath,
        Func<T, string?> title,
        Func<T, DateTime> addedDate,
        bool sortByName,
        bool includeChildren)
    {
        IEnumerable<T> tracks = node.Tracks;
        tracks = sortByName
            ? tracks.OrderBy(item => title(item) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            : tracks.OrderByDescending(addedDate);

        var children = includeChildren
            ? node.Children.Values
                .OrderBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(child => Freeze(child, child.Name, child.RelativePath, title, addedDate, sortByName, includeChildren: true))
                .ToArray()
            : Array.Empty<LocalDirectoryNode<T>>();

        return new LocalDirectoryNode<T>
        {
            Name = name,
            RelativePath = relativePath,
            Children = children,
            Tracks = tracks.ToArray()
        };
    }

    private sealed class BuilderNode<T>
    {
        public BuilderNode(string name, string relativePath)
        {
            Name = name;
            RelativePath = relativePath;
        }

        public string Name { get; }
        public string RelativePath { get; }
        public Dictionary<string, BuilderNode<T>> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<T> Tracks { get; } = new();

        public BuilderNode<T> GetOrAdd(string segment, string relativePath)
        {
            if (!Children.TryGetValue(segment, out var child))
            {
                child = new BuilderNode<T>(segment, relativePath);
                Children.Add(segment, child);
            }

            return child;
        }
    }
}

public sealed class LocalDirectoryNode<T>
{
    public string Name { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public IReadOnlyList<LocalDirectoryNode<T>> Children { get; init; } = Array.Empty<LocalDirectoryNode<T>>();
    public IReadOnlyList<T> Tracks { get; init; } = Array.Empty<T>();

    public int DescendantTrackCount => Tracks.Count + Children.Sum(child => child.DescendantTrackCount);
}
