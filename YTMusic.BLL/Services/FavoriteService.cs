using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Services;

public sealed class FavoriteService : IFavoriteService
{
    private readonly IFavoriteRepository _repository;

    public FavoriteService(IFavoriteRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<FavoriteFolder>> GetFoldersAsync()
    {
        var dbFolders = await _repository.GetFoldersAsync();
        // Real favorite folders only — virtual "已下载" is composed by UI via ILibraryCatalogService.
        return dbFolders.Where(folder => !folder.IsDefault).ToList();
    }

    public Task<FavoriteFolder> CreateFolderAsync(string name) => _repository.CreateFolderAsync(name);

    public async Task RenameFolderAsync(int folderId, string name)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            throw new InvalidOperationException("已下载不能改名。");
        }

        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("名称不能为空。");
        }

        var folders = await _repository.GetFoldersAsync();
        if (folders.Any(folder => folder.Id != folderId && folder.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("已有同名收藏夹。");
        }

        await _repository.RenameFolderAsync(folderId, trimmed);
    }

    public async Task DeleteFolderAsync(int folderId)
    {
        if (FavoriteFolderIds.IsProtectedFolder(folderId))
        {
            throw new InvalidOperationException("Cannot delete built-in folder.");
        }

        await _repository.DeleteFolderAsync(folderId);
    }

    public Task ResetAllAsync() => _repository.ResetAllAsync();

    public async Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? isDownloaded = null)
    {
        if (folderId.HasValue && FavoriteFolderIds.IsDownloadedCatalog(folderId.Value))
        {
            return new List<FavoriteTrack>();
        }

        var tracks = await _repository.GetTracksAsync(
            folderId,
            isDownloaded.HasValue ? isDownloaded.Value : null);

        if (isDownloaded.HasValue && isDownloaded.Value)
        {
            tracks = tracks.Where(t => t.IsDownloaded).ToList();
        }

        return tracks;
    }

    public Task<HashSet<string>> GetFavoritedVideoIdsAsync(IEnumerable<string> videoIds)
        => _repository.GetFavoritedVideoIdsAsync(videoIds);

    public Task<HashSet<string>> GetFavoritedFilePathsAsync(IEnumerable<string> filePaths)
        => _repository.GetFavoritedFilePathsAsync(filePaths);

    public async Task AddToFavoritesAsync(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl,
        int folderId = 1,
        string? localFilePath = null)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            throw new InvalidOperationException("Cannot add favorites to the downloaded catalog folder.");
        }

        await _repository.AddTrackAsync(
            videoId,
            title,
            author,
            thumbnailUrl,
            folderId,
            localFilePath,
            DateTime.UtcNow);
    }

    public async Task RemoveFromFavoritesAsync(string videoId, int folderId = 1)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            throw new InvalidOperationException("Cannot remove items from the downloaded catalog folder.");
        }

        await _repository.RemoveTrackAsync(videoId, folderId);
    }

    public Task UpdateLocalFilePathAsync(string videoId, string? localFilePath)
        => _repository.UpdateLocalFilePathAsync(videoId, localFilePath);

    public Task<bool> IsFavoriteAsync(string videoId, int folderId = 1)
        => _repository.IsFavoriteAsync(videoId, folderId);

    public Task<bool> IsFavoriteInAnyFolderAsync(string videoId)
        => _repository.IsFavoriteInAnyFolderAsync(videoId);

    public Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId)
        => _repository.GetFavoriteFolderIdsForVideoAsync(videoId);

    public async Task<List<string>> GetFavoriteFolderNamesForVideoAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return new List<string>();
        }

        var folderIds = await GetFavoriteFolderIdsForVideoAsync(videoId);
        if (folderIds.Count == 0)
        {
            return new List<string>();
        }

        var folders = await GetFoldersAsync();
        var names = new List<string>();
        foreach (var folderId in folderIds)
        {
            if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
            {
                continue;
            }

            var folder = folders.FirstOrDefault(f => f.Id == folderId);
            if (folder != null && !string.IsNullOrWhiteSpace(folder.Name))
            {
                names.Add(folder.Name);
            }
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<int> GetOrCreateFolderByNameAsync(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException("收藏夹名称不能为空。");
        }

        var trimmed = folderName.Trim();
        var folders = await GetFoldersAsync();
        var existing = folders.FirstOrDefault(folder =>
            !folder.IsDownloadedCatalog
            && folder.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing.Id;
        }

        var created = await CreateFolderAsync(trimmed);
        return created.Id;
    }

    public async Task RestoreFavoriteFoldersForTrackAsync(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl,
        string? localFilePath,
        IReadOnlyList<string>? folderNames)
    {
        if (string.IsNullOrWhiteSpace(videoId) || folderNames == null || folderNames.Count == 0)
        {
            return;
        }

        foreach (var folderName in folderNames)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            var folderId = await GetOrCreateFolderByNameAsync(folderName);
            await AddToFavoritesAsync(videoId, title, author, thumbnailUrl, folderId, localFilePath);
        }
    }

    public Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath)
        => _repository.GetTrackByFilePathAsync(filePath);

    public Task RemoveTrackByFilePathAsync(string filePath)
        => _repository.RemoveTrackByFilePathAsync(filePath);
}
