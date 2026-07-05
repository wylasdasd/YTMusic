using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class FavoritesVM : ViewModelBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ILocalMusicService _localMusicService;
    private readonly IUiNotifier _notifier;
    private readonly IDialogHost _dialogHost;
    private readonly MusicPlayerService _playerService;
    private readonly NavigationManager _navigation;
    private readonly GlobalStateService _globalState;

    public List<FavoriteFolder> Folders { get; private set; } = new();
    public Dictionary<int, int> FolderTrackCounts { get; private set; } = new();
    public Dictionary<int, string?> FolderCoverUrls { get; private set; } = new();
    public Dictionary<int, string?> FolderFirstTrackAuthors { get; private set; } = new();

    public FavoritesVM(
        IFavoriteService favoriteService,
        ILocalMusicService localMusicService,
        IUiNotifier notifier,
        IDialogHost dialogHost,
        MusicPlayerService playerService,
        NavigationManager navigation,
        GlobalStateService globalState)
    {
        _favoriteService = favoriteService;
        _localMusicService = localMusicService;
        _notifier = notifier;
        _dialogHost = dialogHost;
        _playerService = playerService;
        _navigation = navigation;
        _globalState = globalState;
    }

    public async Task InitializeAsync()
    {
        await LoadFoldersAsync();
    }

    [RelayCommand]
    private async Task OpenCreateFolderDialogAsync()
    {
        var name = await _dialogHost.PromptCreateFolderNameAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _favoriteService.CreateFolderAsync(name.Trim());
            _notifier.Success($"已创建收藏夹「{name.Trim()}」");
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            _notifier.Error($"创建收藏夹失败: {ex.Message}");
        }
    }

    public async Task LoadFoldersAsync()
    {
        Folders = await _favoriteService.GetFoldersAsync();
        FolderTrackCounts.Clear();
        FolderCoverUrls.Clear();
        FolderFirstTrackAuthors.Clear();

        foreach (var folder in Folders)
        {
            if (FavoriteFolderIds.IsDownloadedCatalog(folder.Id))
            {
                var downloads = await _localMusicService.GetDownloadedTracksAsync();
                var ordered = downloads
                    .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
                    .OrderByDescending(track => track.DownloadedDate)
                    .ToList();
                var first = ordered.FirstOrDefault();
                FolderTrackCounts[folder.Id] = ordered.Count;
                FolderCoverUrls[folder.Id] = first?.ThumbnailUrl;
                FolderFirstTrackAuthors[folder.Id] = first?.Author;
            }
            else
            {
                var tracks = await _favoriteService.GetTracksAsync(folder.Id, null);
                var first = tracks.OrderByDescending(track => track.AddedDate).FirstOrDefault();
                FolderTrackCounts[folder.Id] = tracks.Count;
                FolderCoverUrls[folder.Id] = first?.ThumbnailUrl;
                FolderFirstTrackAuthors[folder.Id] = first?.Author;
            }
        }

        NotifyChanged();
    }

    public async Task<List<FavoriteTrack>> LoadTracksForFolderAsync(int folderId)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            return await LoadDownloadedCatalogTracksAsync();
        }

        var tracks = await _favoriteService.GetTracksAsync(folderId, null);
        foreach (var track in tracks)
        {
            var downloadedTrack = await _localMusicService.GetDownloadedTrackByVideoIdAsync(track.VideoId);
            if (downloadedTrack?.IsVideo == true)
            {
                track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
            }
        }

        return tracks.OrderByDescending(track => track.AddedDate).ToList();
    }

    private async Task<List<FavoriteTrack>> LoadDownloadedCatalogTracksAsync()
    {
        var downloads = await _localMusicService.GetDownloadedTracksAsync();
        return downloads
            .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
            .OrderByDescending(track => track.DownloadedDate)
            .Select(MapDownloadedTrackToFavorite)
            .ToList();
    }

    private static FavoriteTrack MapDownloadedTrackToFavorite(DownloadedTrack track)
    {
        return new FavoriteTrack
        {
            VideoId = track.VideoId,
            FolderId = FavoriteFolderIds.DownloadedCatalog,
            Title = track.Title,
            Author = track.Author,
            ThumbnailUrl = track.ThumbnailUrl,
            AddedDate = track.DownloadedDate,
            LocalFilePath = track.LocalFilePath,
            LocalVideoFilePath = track.IsVideo ? track.LocalFilePath : null
        };
    }

    public async Task ConfirmClearLocalDownloadsAsync()
    {
        var downloads = await _localMusicService.GetDownloadedTracksAsync();
        if (!downloads.Any())
        {
            return;
        }

        var confirmed = await _dialogHost.ConfirmDeleteLocalFilesAsync(downloads.Count);
        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var file in downloads)
            {
                if (!string.IsNullOrWhiteSpace(file.LocalFilePath))
                {
                    await _favoriteService.RemoveTrackByFilePathAsync(file.LocalFilePath);
                }

                await _localMusicService.RemoveDownloadedTrackAsync(file.VideoId, file.LocalFilePath);
            }

            _notifier.Success($"已删除 {downloads.Count} 个本地文件");
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            _notifier.Error($"删除本地文件失败: {ex.Message}");
        }
    }

    public async Task ConfirmDeleteFolderAsync(FavoriteFolder folder)
    {
        if (FavoriteFolderIds.IsProtectedFolder(folder.Id))
        {
            return;
        }

        var confirmed = await _dialogHost.ConfirmDeleteFolderAsync(folder.Name);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _favoriteService.DeleteFolderAsync(folder.Id);
            _notifier.Success($"已删除收藏夹「{folder.Name}」");
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            _notifier.Error($"删除收藏夹失败: {ex.Message}");
        }
    }

    public void NavigateToFolder(int folderId)
    {
        _navigation.NavigateTo($"/favorites/folder/{folderId}");
    }

    public async Task PlayFolderAsync(int folderId, MusicPlayerService.PlaybackMode mode)
    {
        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            var tracks = await LoadTracksForFolderAsync(folderId);
            if (!tracks.Any())
            {
                return;
            }

            var folderName = Folders.FirstOrDefault(f => f.Id == folderId)?.Name ?? "收藏夹";
            var isDownloadedCatalog = FavoriteFolderIds.IsDownloadedCatalog(folderId);
            var playlist = tracks.Select(t => new PlayingItem
            {
                VideoId = t.VideoId,
                Title = t.Title,
                Author = t.Author,
                ThumbnailUrl = t.ThumbnailUrl,
                LocalFilePath = t.IsDownloaded || isDownloadedCatalog ? t.LocalFilePath : null,
                IsVideo = false
            }).ToList();

            if (await _playerService.PlayPlaylistAsync(playlist, 0, folderName, mode))
            {
                _navigation.NavigateTo("/player");
            }
        }
        finally
        {
            _globalState.HideLoading();
            NotifyChanged();
        }
    }
}
