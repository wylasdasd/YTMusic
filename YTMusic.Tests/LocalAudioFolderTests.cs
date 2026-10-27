using YTMusic.BLL.Models;

namespace YTMusic.Tests;

public class LocalAudioFolderTests
{
    [Fact]
    public void FindAudioFiles_IncludesNestedAudioAndSkipsOtherExtensions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ytmusic-import-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Album");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, "root.mp3"), "a");
        File.WriteAllText(Path.Combine(nested, "song.flac"), "a");
        File.WriteAllText(Path.Combine(nested, "notes.txt"), "a");
        var hidden = Path.Combine(root, "hidden.mp3");
        File.WriteAllText(hidden, "a");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        try
        {
            var files = LocalAudioFolder.FindAudioFiles(root);

            Assert.Equal(
                new[]
                {
                    Path.GetFullPath(Path.Combine(nested, "song.flac")),
                    Path.GetFullPath(Path.Combine(root, "root.mp3"))
                },
                files);
        }
        finally
        {
            File.SetAttributes(hidden, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateVideoId_IsStableAndPrefixed()
    {
        var path = Path.Combine(Path.GetTempPath(), "song.mp3");

        var first = LocalAudioFolder.CreateVideoId(path);
        var second = LocalAudioFolder.CreateVideoId(path);

        Assert.Equal(first, second);
        Assert.StartsWith("local:", first);
    }

    [Fact]
    public void AllocateFolderName_AppendsIndexWhenNameExists()
    {
        var name = LocalAudioFolder.AllocateFolderName("Music", ["music", "Music (2)"]);

        Assert.Equal("Music (3)", name);
    }
}
