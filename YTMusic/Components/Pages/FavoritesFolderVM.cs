using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public enum FavoriteTrackSortMode
{
    Time = 0,
    Name = 1,
    Downloaded = 2,
    NotDownloaded = 3
}

public sealed partial class FavoritesFolderVM : ViewModelBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ILibraryCatalogService _libraryCatalog;
    private readonly ILocalMusicService _localMusicService;
    private readonly IDownloadMusicDirectoryProvider _downloadDirectoryProvider;
    private readonly IUiNotifier _notifier;
    private readonly IDialogHost _dialogHost;
    private readonly MusicPlayerService _playerService;
    private readonly LocalFileDeleteCoordinator _localFileDelete;
    private readonly NavigationManager _navigation;
    private readonly GlobalStateService _globalState;
    private List<FavoriteTrack> _rawTracks = new();

    public int FolderId { get; private set; }
    public string? FolderName { get; private set; }
    public List<FavoriteTrack> Tracks { get; private set; } = new();
    public IReadOnlyList<LocalDirectoryNode<FavoriteTrack>> DirectoryNodes { get; private set; } = Array.Empty<LocalDirectoryNode<FavoriteTrack>>();
    public HashSet<string> FavoritedVideoIds { get; private set; } = new(StringComparer.Ordinal);
    public FavoriteTrackSortMode SortMode { get; set; } = FavoriteTrackSortMode.Time;
    public bool IsLoading { get; private set; }

    public FavoriteFolderKind FolderKind { get; private set; } = FavoriteFolderKind.User;
    public bool IsDownloadedCatalog => FolderKind == FavoriteFolderKind.DownloadedCatalog;
    public bool IsLocalLibrary => FolderKind == FavoriteFolderKind.LocalImport;
    public string? LocalRootPath { get; private set; }
    public bool UsesDirectoryGroups => FolderKind is FavoriteFolderKind.DownloadedCatalog or FavoriteFolderKind.LocalImport;

    public FavoritesFolderVM(
        IFavoriteService favoriteService,
        ILibraryCatalogService libraryCatalog,
        ILocalMusicService localMusicService,
        IDownloadMusicDirectoryProvider downloadDirectoryProvider,
        IUiNotifier notifier,
        IDialogHost dialogHost,
        MusicPlayerService playerService,
        LocalFileDeleteCoordinator localFileDelete,
        NavigationManager navigation,
        GlobalStateService globalState)
    {
        _favoriteService = favoriteService;
        _libraryCatalog = libraryCatalog;
        _localMusicService = localMusicService;
        _downloadDirectoryProvider = downloadDirectoryProvider;
        _notifier = notifier;
        _dialogHost = dialogHost;
        _playerService = playerService;
        _localFileDelete = localFileDelete;
        _navigation = navigation;
        _globalState = globalState;
    }

    public async Task InitializeAsync(int folderId)
    {
        FolderId = folderId;
        SortMode = FavoriteTrackSortMode.Time;

        var folders = await _favoriteService.GetFoldersAsync();
        var folder = folders.FirstOrDefault(f => f.Id == folderId);
        FolderKind = FavoriteFolderKindHelper.Resolve(folderId, folder);
        FolderName = folder?.Name;
        LocalRootPath = folder?.LocalRootPath;

        if (FolderKind == FavoriteFolderKind.DownloadedCatalog)
        {
            FolderName = "已下载";
        }
        else if (FolderName == null)
        {
            _notifier.Warning("收藏夹不存在。");
            return;
        }

        await LoadTracksAsync();
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

    public async Task LoadTracksAsync()
    {
        IsLoading = true;
        NotifyChanged();

        try
        {
            if (FolderKind == FavoriteFolderKind.DownloadedCatalog)
            {
                _rawTracks = await LoadDownloadedCatalogTracksAsync();
            }
            else
            {
                _rawTracks = await _favoriteService.GetTracksAsync(FolderId, null);
                await AttachDownloadedVideoPathsAsync(_rawTracks);
            }

            ApplyTrackOrdering();
            await RefreshFavoritedVideoIdsAsync();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to load favorites: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    public bool IsTrackFavoritedAnywhere(FavoriteTrack track)
    {
        if (IsDownloadedCatalog)
        {
            return FavoritedVideoIds.Contains(track.VideoId);
        }

        return track.FolderId == FolderId;
    }

    private async Task RefreshFavoritedVideoIdsAsync()
    {
        FavoritedVideoIds.Clear();
        if (!_rawTracks.Any())
        {
            return;
        }

        var videoIds = _rawTracks
            .Select(track => track.VideoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray();

        if (videoIds.Length == 0)
        {
            return;
        }

        var favorited = await _favoriteService.GetFavoritedVideoIdsAsync(videoIds);
        foreach (var videoId in favorited)
        {
            FavoritedVideoIds.Add(videoId);
        }
    }

    public Task SetSortModeAsync(FavoriteTrackSortMode sortMode)
    {
        SortMode = sortMode;
        ApplyTrackOrdering();
        NotifyChanged();
        return Task.CompletedTask;
    }

    public async Task PlayDirectoryAsync(LocalDirectoryNode<FavoriteTrack> node)
    {
        var tracks = LocalDirectoryTree.Flatten(new[] { node });
        var playlist = BuildPlaylist(tracks);
        if (playlist.Count == 0)
        {
            _notifier.Warning("没有可播放的文件。");
            return;
        }

        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            if (await _playerService.PlayPlaylistAsync(
                    playlist,
                    0,
                    node.Name,
                    PlaybackMode.Sequential))
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

    private void ApplyTrackOrdering()
    {
        if (UsesDirectoryGroups)
        {
            var libraryRoot = FolderKind == FavoriteFolderKind.LocalImport
                ? LocalRootPath
                : _downloadDirectoryProvider.GetDownloadedMusicDirectory();
            DirectoryNodes = LocalDirectoryTree.Build(
                _rawTracks,
                libraryRoot,
                SortMode == FavoriteTrackSortMode.Name);
            Tracks = LocalDirectoryTree.Flatten(DirectoryNodes);
            return;
        }

        DirectoryNodes = Array.Empty<LocalDirectoryNode<FavoriteTrack>>();
        IEnumerable<FavoriteTrack> query = _rawTracks;

        switch (SortMode)
        {
            case FavoriteTrackSortMode.Downloaded:
                query = query.Where(track => track.IsDownloaded)
                    .OrderByDescending(track => track.AddedDate);
                break;
            case FavoriteTrackSortMode.NotDownloaded:
                query = query.Where(track => !track.IsDownloaded)
                    .OrderByDescending(track => track.AddedDate);
                break;
            case FavoriteTrackSortMode.Name:
                query = query.OrderBy(track => track.Title, StringComparer.CurrentCultureIgnoreCase);
                break;
            default:
                query = query.OrderByDescending(track => track.AddedDate);
                break;
        }

        Tracks = query.ToList();
    }

    private async Task<List<FavoriteTrack>> LoadDownloadedCatalogTracksAsync()
    {
        var items = await _libraryCatalog.GetDownloadedItemsAsync();
        return items.Select(item => item.AsFavoriteTrack()).ToList();
    }

    public async Task DeleteDownloadedFileAsync(FavoriteTrack track)
    {
        try
        {
            var removedFile = !string.IsNullOrWhiteSpace(track.LocalFilePath) && track.IsDownloaded;
            if (removedFile)
            {
                await _localFileDelete.ClearDownloadedLocalFileAsync(
                    _localMusicService,
                    track.VideoId,
                    track.LocalFilePath);
                await _favoriteService.UpdateLocalFilePathAsync(track.VideoId, null);
            }

            await _favoriteService.RemoveFromFavoritesAsync(track.VideoId, FolderId);
            _rawTracks.RemoveAll(item => item.VideoId == track.VideoId);
            ApplyTrackOrdering();
            _notifier.Success(removedFile
                ? $"已删除「{track.Title}」的收藏和本地文件"
                : $"已从收藏夹移除「{track.Title}」");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"删除失败: {ex.Message}");
        }
    }

    public async Task DeleteLocalFileAsync(FavoriteTrack track)
    {
        if (!UsesDirectoryGroups || string.IsNullOrWhiteSpace(track.LocalFilePath))
        {
            return;
        }

        try
        {
            await _favoriteService.RemoveTrackByFilePathAsync(track.LocalFilePath);
            await _localFileDelete.ClearDownloadedLocalFileAsync(
                _localMusicService,
                track.VideoId,
                track.LocalFilePath);
            _rawTracks.Remove(track);
            ApplyTrackOrdering();
            _notifier.Success($"已删除「{track.Title}」");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"删除本地文件失败: {ex.Message}");
        }
    }

    public async Task OpenFavoriteFolderDialogAsync(FavoriteTrack track)
    {
        await _dialogHost.ShowFavoriteFolderDialogAsync(
            new FavoritePickRequest
            {
                VideoId = track.VideoId,
                Title = track.Title,
                Author = track.Author,
                ThumbnailUrl = track.ThumbnailUrl,
                LocalFilePath = track.LocalFilePath
            },
            "管理收藏夹");

        if (IsDownloadedCatalog)
        {
            await RefreshFavoritedVideoIdsAsync();
        }
        else
        {
            var stillInCurrentFolder = await _favoriteService.IsFavoriteAsync(track.VideoId, FolderId);
            if (!stillInCurrentFolder)
            {
                _rawTracks.Remove(track);
                ApplyTrackOrdering();
            }
        }

        NotifyChanged();
    }

    public void NavigateBackToFolderList()
    {
        _navigation.NavigateTo("/favorites");
    }

    public async Task RenameCurrentFolderAsync()
    {
        if (IsDownloadedCatalog || string.IsNullOrWhiteSpace(FolderName))
        {
            return;
        }

        var name = await _dialogHost.PromptRenameFolderNameAsync(FolderName);
        if (string.IsNullOrWhiteSpace(name) || name.Equals(FolderName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await _favoriteService.RenameFolderAsync(FolderId, name);
            FolderName = name.Trim();
            _notifier.Success($"已重命名为「{FolderName}」");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"重命名失败: {ex.Message}");
        }
    }

    public async Task PlayTrackAsync(FavoriteTrack track)
    {
        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            if (!Tracks.Any())
            {
                return;
            }

            var playlist = BuildPlaylist(Tracks);
            if (playlist.Count == 0)
            {
                _notifier.Warning("没有可播放的文件。");
                return;
            }

            var startIndex = playlist.FindIndex(item =>
                item.VideoId == track.VideoId &&
                string.Equals(item.LocalFilePath, track.LocalFilePath, StringComparison.OrdinalIgnoreCase));
            if (startIndex == -1)
            {
                if (IsLocalLibrary)
                {
                    _notifier.Warning("文件不存在。");
                    return;
                }

                startIndex = 0;
            }

            var folderName = FolderName ?? "收藏夹";

            if (await _playerService.PlayPlaylistAsync(
                    playlist,
                    startIndex,
                    folderName,
                    PlaybackMode.Sequential))
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

    public async Task PlayLocalVideoAsync(FavoriteTrack track)
    {
        if (!track.HasDownloadedVideo || string.IsNullOrWhiteSpace(track.LocalVideoFilePath))
        {
            return;
        }

        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            if (await _playerService.PlayLocalFileAsync(
                    track.LocalVideoFilePath,
                    track.Title,
                    true,
                    track.Author,
                    track.ThumbnailUrl,
                    track.VideoId))
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

    private List<PlayingItem> BuildPlaylist(IEnumerable<FavoriteTrack> tracks)
    {
        return PlayingItemMapper.FromFavoriteTracks(
            tracks,
            forceIncludeLocalPath: UsesDirectoryGroups,
            requireExistingLocalFile: FolderKind == FavoriteFolderKind.LocalImport);
    }
}
