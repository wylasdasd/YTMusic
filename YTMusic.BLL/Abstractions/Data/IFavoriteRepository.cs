using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions.Data;

public interface IFavoriteRepository
{
    Task<List<FavoriteFolder>> GetFoldersAsync();
    Task<FavoriteFolder> CreateFolderAsync(string name);
    Task DeleteFolderAsync(int folderId);
    Task ResetAllAsync();

    Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? hasLocalFilePath = null);
    Task<HashSet<string>> GetFavoritedVideoIdsAsync(IEnumerable<string> videoIds);
    Task<HashSet<string>> GetFavoritedFilePathsAsync(IEnumerable<string> filePaths);
    Task AddTrackAsync(string videoId, string title, string author, string? thumbnailUrl, int folderId, string? localFilePath, DateTime addedDate);
    Task RemoveTrackAsync(string videoId, int folderId);
    Task UpdateLocalFilePathAsync(string videoId, string? localFilePath);
    Task<bool> IsFavoriteAsync(string videoId, int folderId);
    Task<bool> IsFavoriteInAnyFolderAsync(string videoId);
    Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId);
    Task<string?> GetLocalFilePathAsync(string videoId, int folderId);
    Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath);
    Task RemoveTrackByFilePathAsync(string filePath);
}
