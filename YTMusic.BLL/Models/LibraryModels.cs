namespace YTMusic.BLL.Models;

/// <summary>
/// Local library entry (downloaded or imported file), distinct from user favorite tracks.
/// </summary>
public sealed class LibraryItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string LocalFilePath { get; set; } = string.Empty;
    public bool IsVideo { get; set; }
    public DateTime AddedDate { get; set; }

    /// <summary>
    /// Adapts a library item for Favorites-folder UI that still binds <see cref="FavoriteTrack"/>.
    /// </summary>
    public FavoriteTrack AsFavoriteTrack(int folderId = FavoriteFolderIds.DownloadedCatalog)
    {
        return new FavoriteTrack
        {
            VideoId = VideoId,
            FolderId = folderId,
            Title = Title,
            Author = Author,
            ThumbnailUrl = ThumbnailUrl,
            AddedDate = AddedDate,
            LocalFilePath = LocalFilePath,
            LocalVideoFilePath = IsVideo ? LocalFilePath : null
        };
    }
}

public enum FavoriteFolderKind
{
    User = 0,
    DownloadedCatalog = 1,
    LocalImport = 2
}

public static class FavoriteFolderKindHelper
{
    public static FavoriteFolderKind Resolve(int folderId, FavoriteFolder? folder)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            return FavoriteFolderKind.DownloadedCatalog;
        }

        if (folder?.IsLocalImport == true)
        {
            return FavoriteFolderKind.LocalImport;
        }

        return FavoriteFolderKind.User;
    }

    public static FavoriteFolder CreateDownloadedCatalogFolder()
    {
        return new FavoriteFolder
        {
            Id = FavoriteFolderIds.DownloadedCatalog,
            Name = "已下载",
            IsDownloadedCatalog = true
        };
    }
}
