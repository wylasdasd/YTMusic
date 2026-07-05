using CommonTool.FileHelps;
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

        var defaultFolder = dbFolders.FirstOrDefault(f => f.Id == FavoriteFolderIds.Default)
            ?? new FavoriteFolder { Id = FavoriteFolderIds.Default, Name = AppGlobal.Favorites.DefaultFolderName, IsDefault = true };

        var folders = new List<FavoriteFolder> { defaultFolder };
        folders.Add(new FavoriteFolder
        {
            Id = FavoriteFolderIds.DownloadedCatalog,
            Name = "已下载",
            IsDownloadedCatalog = true
        });
        folders.AddRange(dbFolders.Where(f => f.Id != FavoriteFolderIds.Default));
        return folders;
    }

    public Task<FavoriteFolder> CreateFolderAsync(string name) => _repository.CreateFolderAsync(name);

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
            return FavoriteFolderIds.Default;
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

    public async Task DeleteDownloadedFileAndRecordAsync(string videoId, int folderId = 1)
    {
        var path = await _repository.GetLocalFilePathAsync(videoId, folderId);
        if (!string.IsNullOrWhiteSpace(path))
        {
            FileHelp.DeleteIfExists(path);
        }

        await RemoveFromFavoritesAsync(videoId, folderId);
    }

    public Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath)
        => _repository.GetTrackByFilePathAsync(filePath);

    public Task RemoveTrackByFilePathAsync(string filePath)
        => _repository.RemoveTrackByFilePathAsync(filePath);
}
