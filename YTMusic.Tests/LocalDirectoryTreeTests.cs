using YTMusic.BLL.Models;

namespace YTMusic.Tests;

public class LocalDirectoryTreeTests
{
    [Fact]
    public void Build_GroupsTracksByDirectoryUnderLibraryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ytmusic-lib");
        var tracks = new[]
        {
            Track("root-b", Path.Combine(root, "b.mp3"), new DateTime(2026, 1, 2)),
            Track("root-a", Path.Combine(root, "a.mp3"), new DateTime(2026, 1, 3)),
            Track("nested", Path.Combine(root, "Album B", "Disc", "nested.mp3"), new DateTime(2026, 1, 1)),
            Track("album-b", Path.Combine(root, "Album A", "b.mp3"), new DateTime(2026, 1, 4)),
            Track("album-a", Path.Combine(root, "Album A", "a.mp3"), new DateTime(2026, 1, 5))
        };

        var nodes = LocalDirectoryTree.Build(tracks, root, sortByName: true);

        Assert.Equal(new[] { new DirectoryInfo(root).Name, "Album A", "Album B" }, nodes.Select(node => node.Name).ToArray());
        Assert.Equal(new[] { "root-a", "root-b" }, nodes[0].Tracks.Select(track => track.Title).ToArray());
        Assert.Equal(new[] { "album-a", "album-b" }, nodes[1].Tracks.Select(track => track.Title).ToArray());
        Assert.Empty(nodes[2].Tracks);
        var disc = Assert.Single(nodes[2].Children);
        Assert.Equal("Disc", disc.Name);
        Assert.Equal("nested", Assert.Single(disc.Tracks).Title);
        Assert.Equal(1, nodes[2].DescendantTrackCount);
    }

    [Fact]
    public void Build_SortsTracksInADirectoryByAddedDateWhenNameSortIsOff()
    {
        var root = Path.Combine(Path.GetTempPath(), "ytmusic-lib-time");
        var tracks = new[]
        {
            Track("older", Path.Combine(root, "Album", "older.mp3"), new DateTime(2026, 1, 1)),
            Track("newer", Path.Combine(root, "Album", "newer.mp3"), new DateTime(2026, 2, 1))
        };

        var album = Assert.Single(LocalDirectoryTree.Build(tracks, root, sortByName: false));

        Assert.Equal(new[] { "newer", "older" }, album.Tracks.Select(track => track.Title).ToArray());
    }

    [Fact]
    public void Build_UsesParentFolderNameWhenFileIsOutsideLibraryRoot()
    {
        var library = Path.Combine(Path.GetTempPath(), "ytmusic-lib-inside");
        var outside = Path.Combine(Path.GetTempPath(), "somewhere-else", "Loose", "song.mp3");

        var node = Assert.Single(LocalDirectoryTree.Build(new[] { Track("song", outside, DateTime.UtcNow) }, library, sortByName: true));

        Assert.Equal("Loose", node.Name);
        Assert.Equal("song", Assert.Single(node.Tracks).Title);
    }

    [Fact]
    public void Flatten_VisitsNestedDirectoriesBeforeTracksInTheSameDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "ytmusic-lib-flat");
        var tracks = new[]
        {
            Track("root", Path.Combine(root, "root.mp3"), DateTime.UtcNow),
            Track("direct", Path.Combine(root, "Album", "direct.mp3"), DateTime.UtcNow),
            Track("nested", Path.Combine(root, "Album", "Disc", "nested.mp3"), DateTime.UtcNow)
        };

        var flat = LocalDirectoryTree.Flatten(LocalDirectoryTree.Build(tracks, root, sortByName: true));

        Assert.Equal(new[] { "root", "nested", "direct" }, flat.Select(track => track.Title).ToArray());
    }

    private static FavoriteTrack Track(string title, string path, DateTime addedDate)
    {
        return new FavoriteTrack
        {
            VideoId = title,
            Title = title,
            LocalFilePath = path,
            AddedDate = addedDate
        };
    }
}
