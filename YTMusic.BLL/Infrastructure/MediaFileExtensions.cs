namespace YTMusic.BLL.Infrastructure;

/// <summary>AList / 上传等场景共用的本地媒体扩展名判断。</summary>
public static class MediaFileExtensions
{
    private static readonly HashSet<string> Media = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus",
        ".mp4", ".mkv", ".webm", ".mov", ".avi"
    };

    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi"
    };

    public static bool IsMediaFile(string fileName)
        => Media.Contains(Path.GetExtension(fileName));

    public static bool IsVideoFile(string fileName)
        => Video.Contains(Path.GetExtension(fileName));
}
