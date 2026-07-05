using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IFavoriteService
{
    Task<List<FavoriteFolder>> GetFoldersAsync();
    Task<FavoriteFolder> CreateFolderAsync(string name);
    Task DeleteFolderAsync(int folderId);
    Task ResetAllAsync();

    Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? isDownloaded = null);
    Task<HashSet<string>> GetFavoritedVideoIdsAsync(IEnumerable<string> videoIds);
    Task<HashSet<string>> GetFavoritedFilePathsAsync(IEnumerable<string> filePaths);
    Task AddToFavoritesAsync(string videoId, string title, string author, string? thumbnailUrl, int folderId = 1, string? localFilePath = null);
    Task RemoveFromFavoritesAsync(string videoId, int folderId = 1);
    Task UpdateLocalFilePathAsync(string videoId, string? localFilePath);
    Task<bool> IsFavoriteAsync(string videoId, int folderId = 1);
    Task<bool> IsFavoriteInAnyFolderAsync(string videoId);
    Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId);
    Task<List<string>> GetFavoriteFolderNamesForVideoAsync(string videoId);
    Task<int> GetOrCreateFolderByNameAsync(string folderName);
    Task RestoreFavoriteFoldersForTrackAsync(string videoId, string title, string author, string? thumbnailUrl, string? localFilePath, IReadOnlyList<string>? folderNames);
    Task DeleteDownloadedFileAndRecordAsync(string videoId, int folderId = 1);
    Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath);
    Task RemoveTrackByFilePathAsync(string filePath);
}
