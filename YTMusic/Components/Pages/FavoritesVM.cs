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
    private readonly ILibraryCatalogService _libraryCatalog;
    private readonly ILocalFolderImportService _localFolderImportService;
    private readonly IFolderPicker _folderPicker;
    private readonly ILocalMusicService _localMusicService;
    private readonly IDownloadMusicDirectoryProvider _downloadDirectoryProvider;
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
        ILibraryCatalogService libraryCatalog,
        ILocalFolderImportService localFolderImportService,
        IFolderPicker folderPicker,
        ILocalMusicService localMusicService,
        IDownloadMusicDirectoryProvider downloadDirectoryProvider,
        IUiNotifier notifier,
        IDialogHost dialogHost,
        MusicPlayerService playerService,
        NavigationManager navigation,
        GlobalStateService globalState)
    {
        _favoriteService = favoriteService;
        _libraryCatalog = libraryCatalog;
        _localFolderImportService = localFolderImportService;
        _folderPicker = folderPicker;
        _localMusicService = localMusicService;
        _downloadDirectoryProvider = downloadDirectoryProvider;
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

    public async Task RenameFolderAsync(FavoriteFolder folder)
    {
        if (FavoriteFolderIds.IsDownloadedCatalog(folder.Id))
        {
            return;
        }

        var name = await _dialogHost.PromptRenameFolderNameAsync(folder.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Equals(folder.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await _favoriteService.RenameFolderAsync(folder.Id, name);
            _notifier.Success($"已重命名为「{name.Trim()}」");
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            _notifier.Error($"重命名失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportLocalFolderAsync()
    {
        try
        {
            var pick = await _folderPicker.PickAsync("选择要导入的音乐文件夹");
            if (!pick.IsSupported)
            {
                _notifier.Warning("当前平台不能选择文件夹。");
                return;
            }

            if (string.IsNullOrWhiteSpace(pick.Path))
            {
                return;
            }

            _globalState.ShowLoading();
            NotifyChanged();
            var result = await _localFolderImportService.ImportAsync(pick.Path);
            if (result == null)
            {
                _notifier.Warning("这个文件夹里没有可播放的音频。");
                return;
            }

            _notifier.Success($"已导入 {result.TrackCount} 首到「{result.Folder.Name}」");
            await LoadFoldersAsync();
            _navigation.NavigateTo($"/favorites/folder/{result.Folder.Id}");
        }
        catch (Exception ex)
        {
            _notifier.Error($"导入文件夹失败: {ex.Message}");
        }
        finally
        {
            _globalState.HideLoading();
            NotifyChanged();
        }
    }

    public async Task LoadFoldersAsync()
    {
        var userFolders = await _favoriteService.GetFoldersAsync();
        Folders = new List<FavoriteFolder> { FavoriteFolderKindHelper.CreateDownloadedCatalogFolder() };
        Folders.AddRange(userFolders);

        FolderTrackCounts.Clear();
        FolderCoverUrls.Clear();
        FolderFirstTrackAuthors.Clear();

        foreach (var folder in Folders)
        {
            var kind = FavoriteFolderKindHelper.Resolve(folder.Id, folder);
            if (kind == FavoriteFolderKind.DownloadedCatalog)
            {
                var downloads = await _libraryCatalog.GetDownloadedItemsAsync();
                var first = downloads.FirstOrDefault();
                FolderTrackCounts[folder.Id] = downloads.Count;
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
        var kind = ResolveFolderKind(folderId);
        if (kind == FavoriteFolderKind.DownloadedCatalog)
        {
            var downloads = await LoadDownloadedCatalogTracksAsync();
            var nodes = LocalDirectoryTree.Build(
                downloads,
                _downloadDirectoryProvider.GetDownloadedMusicDirectory(),
                sortByName: true);
            return LocalDirectoryTree.Flatten(nodes);
        }

        var folder = Folders.FirstOrDefault(item => item.Id == folderId);
        if (kind == FavoriteFolderKind.LocalImport)
        {
            var imported = await _favoriteService.GetTracksAsync(folderId, null);
            var importedNodes = LocalDirectoryTree.Build(imported, folder?.LocalRootPath, sortByName: true);
            return LocalDirectoryTree.Flatten(importedNodes);
        }

        var tracks = await _favoriteService.GetTracksAsync(folderId, null);
        await AttachDownloadedVideoPathsAsync(tracks);

        return tracks.OrderByDescending(track => track.AddedDate).ToList();
    }

    private FavoriteFolderKind ResolveFolderKind(int folderId)
    {
        var folder = Folders.FirstOrDefault(item => item.Id == folderId);
        return FavoriteFolderKindHelper.Resolve(folderId, folder);
    }

    private async Task AttachDownloadedVideoPathsAsync(IReadOnlyList<FavoriteTrack> tracks)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        var downloaded = await _localMusicService.GetDownloadedTracksByVideoIdsAsync(tracks.Select(track => track.VideoId));
        foreach (var track in tracks)
        {
            if (downloaded.TryGetValue(track.VideoId, out var downloadedTrack) && downloadedTrack.IsVideo)
            {
                track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
            }
        }
    }

    private async Task<List<FavoriteTrack>> LoadDownloadedCatalogTracksAsync()
    {
        var items = await _libraryCatalog.GetDownloadedItemsAsync();
        return items.Select(item => item.AsFavoriteTrack()).ToList();
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

                await _localMusicService.ClearDownloadedLocalFileAsync(file.VideoId, file.LocalFilePath);
            }

            _notifier.Success($"已清空 {downloads.Count} 首，已下载收藏夹仍保留");
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

    public async Task PlayFolderAsync(int folderId, PlaybackMode mode)
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

            var folder = Folders.FirstOrDefault(f => f.Id == folderId);
            var folderName = folder?.Name ?? "收藏夹";
            var kind = ResolveFolderKind(folderId);
            var playlist = PlayingItemMapper.FromFavoriteTracks(
                tracks,
                forceIncludeLocalPath: kind is FavoriteFolderKind.DownloadedCatalog or FavoriteFolderKind.LocalImport,
                requireExistingLocalFile: kind == FavoriteFolderKind.LocalImport);
            if (kind == FavoriteFolderKind.LocalImport && playlist.Count == 0)
            {
                _notifier.Warning("没有可播放的文件。");
                return;
            }

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
