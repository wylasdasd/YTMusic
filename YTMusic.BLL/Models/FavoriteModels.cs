namespace YTMusic.BLL.Models;

public static class FavoriteFolderIds
{
    public const int Default = 1;
    public const int DownloadedCatalog = -1;

    public static bool IsDownloadedCatalog(int folderId) => folderId == DownloadedCatalog;

    public static bool IsProtectedFolder(int folderId)
        => folderId == Default || folderId == DownloadedCatalog;
}

public class FavoriteFolder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsDownloadedCatalog { get; set; }
}

public class FavoriteTrack
{
    public string VideoId { get; set; } = string.Empty;
    public int FolderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public DateTime AddedDate { get; set; }
    public string? LocalFilePath { get; set; }
    public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath);
    public string? LocalVideoFilePath { get; set; }
    public bool HasDownloadedVideo => !string.IsNullOrEmpty(LocalVideoFilePath) && File.Exists(LocalVideoFilePath);
}
