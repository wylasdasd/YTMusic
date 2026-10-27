using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;

namespace YTMusic.ViewModels.Shared;

public sealed partial class PlayerVM : ViewModelBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ILocalMusicService _localMusicService;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IDialogHost _dialogHost;
    private readonly IUiNotifier _notifier;
    private readonly MusicPlayerService _playerService;
    private readonly NavigationManager _navigation;
    private readonly GlobalStateService _globalState;

    [ObservableProperty]
    private bool _isFavorite;

    private string? _currentCheckedVideoId;

    public PlayerVM(
        IFavoriteService favoriteService,
        ILocalMusicService localMusicService,
        IDownloadManagerService downloadManager,
        IDialogHost dialogHost,
        IUiNotifier notifier,
        MusicPlayerService playerService,
        NavigationManager navigation,
        GlobalStateService globalState)
    {
        _favoriteService = favoriteService;
        _localMusicService = localMusicService;
        _downloadManager = downloadManager;
        _dialogHost = dialogHost;
        _notifier = notifier;
        _playerService = playerService;
        _navigation = navigation;
        _globalState = globalState;
    }

    public MusicPlayerService PlayerService => _playerService;

    public async Task OnPlayerStateChangedAsync()
    {
        if (_playerService.IsCurrentStreamVideo)
        {
            if (!(OperatingSystem.IsAndroid() && _playerService.UseNativeVideoPlayback))
            {
                _navigation.NavigateTo("/player/video", replace: true);
            }

            return;
        }

        await RefreshFavoriteStatusAsync(_playerService.CurrentVideo);
    }

    public async Task RefreshFavoriteStatusAsync(PlayingItem? current)
    {
        if (current == null)
        {
            IsFavorite = false;
            _currentCheckedVideoId = null;
            return;
        }

        var trackingId = !string.IsNullOrEmpty(current.LocalFilePath) ? current.LocalFilePath : current.VideoId;
        if (_currentCheckedVideoId == trackingId)
        {
            return;
        }

        _currentCheckedVideoId = trackingId;

        if (!string.IsNullOrEmpty(current.LocalFilePath))
        {
            var track = await _favoriteService.GetTrackByFilePathAsync(current.LocalFilePath);
            IsFavorite = track != null;
        }
        else
        {
            var folders = await _favoriteService.GetFavoriteFolderIdsForVideoAsync(current.VideoId);
            IsFavorite = folders.Any();
        }
    }

    [RelayCommand]
    private async Task OpenFavoriteDialogAsync()
    {
        var currentVideo = _playerService.CurrentVideo;
        if (currentVideo == null)
        {
            return;
        }

        var videoId = currentVideo.VideoId;

        if (!string.IsNullOrEmpty(currentVideo.LocalFilePath))
        {
            var existingTrack = await _favoriteService.GetTrackByFilePathAsync(currentVideo.LocalFilePath);
            videoId = existingTrack?.VideoId ?? Guid.NewGuid().ToString("N")[..11];
        }

        await _dialogHost.ShowFavoriteFolderDialogAsync(
            new FavoritePickRequest
            {
                VideoId = videoId,
                Title = currentVideo.Title,
                Author = currentVideo.Author,
                ThumbnailUrl = currentVideo.ThumbnailUrl,
                LocalFilePath = currentVideo.LocalFilePath
            },
            "Add to Favorite Folder");

        _currentCheckedVideoId = null;
        await RefreshFavoriteStatusAsync(currentVideo);
    }

    public void Download(bool isVideo)
    {
        var currentVideo = _playerService.CurrentVideo;
        if (currentVideo == null)
        {
            return;
        }

        if (currentVideo.VideoId == "local")
        {
            _notifier.Warning("This file is already downloaded.");
            return;
        }

        if (isVideo)
        {
            _notifier.Warning(YTMusic.BLL.AppGlobal.Network.VideoDownloadNotSupported);
            return;
        }

        _downloadManager.StartDownload(currentVideo.VideoId, currentVideo.Title, isVideo);
        _notifier.Success($"Started downloading: {currentVideo.Title}");
    }

    [RelayCommand]
    private async Task DeleteLocalAndUnfavoriteAsync()
    {
        var current = _playerService.CurrentVideo;
        if (current == null || _playerService.IsSwitchingTrack)
        {
            return;
        }

        var path = current.LocalFilePath;
        var videoId = current.VideoId;
        var downloaded = !string.IsNullOrWhiteSpace(path)
            ? await _localMusicService.GetDownloadedTrackByFilePathAsync(path)
            : null;
        if (downloaded == null
            && !string.IsNullOrWhiteSpace(videoId)
            && !string.Equals(videoId, "local", StringComparison.Ordinal))
        {
            downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(videoId);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = downloaded?.LocalFilePath;
        }

        var favoriteVideoId = !string.IsNullOrWhiteSpace(downloaded?.VideoId)
            && !string.Equals(downloaded.VideoId, "local", StringComparison.Ordinal)
            ? downloaded.VideoId
            : videoId;
        var hasFile = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        var favorited = IsFavorite;
        if (!favorited && !string.IsNullOrWhiteSpace(path))
        {
            favorited = await _favoriteService.GetTrackByFilePathAsync(path) != null;
        }

        if (!favorited
            && !string.IsNullOrWhiteSpace(favoriteVideoId)
            && !string.Equals(favoriteVideoId, "local", StringComparison.Ordinal))
        {
            favorited = await _favoriteService.IsFavoriteInAnyFolderAsync(favoriteVideoId);
        }

        if (!hasFile && !favorited)
        {
            _notifier.Warning("当前曲目没有本地文件，也未收藏。");
            return;
        }

        if (!await _dialogHost.ConfirmDeleteLocalAndFavoriteAsync(current.Title))
        {
            return;
        }

        try
        {
            var released = await _playerService.ReleaseIfPlayingFileAsync(path);
            if (!string.IsNullOrWhiteSpace(path))
            {
                await _favoriteService.RemoveTrackByFilePathAsync(path);
            }

            if (!string.IsNullOrWhiteSpace(favoriteVideoId)
                && !string.Equals(favoriteVideoId, "local", StringComparison.Ordinal))
            {
                var folderIds = await _favoriteService.GetFavoriteFolderIdsForVideoAsync(favoriteVideoId);
                foreach (var folderId in folderIds)
                {
                    if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
                    {
                        continue;
                    }

                    await _favoriteService.RemoveFromFavoritesAsync(favoriteVideoId, folderId);
                }
            }

            if (hasFile)
            {
                if (!string.IsNullOrWhiteSpace(downloaded?.VideoId)
                    && !string.Equals(downloaded.VideoId, "local", StringComparison.Ordinal))
                {
                    await _localMusicService.RemoveDownloadedTrackAsync(downloaded.VideoId, path!);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path!);
                }
            }

            if (released)
            {
                var removedPath = path;
                await _playerService.ContinueAfterDroppingAsync(item =>
                    !string.IsNullOrWhiteSpace(removedPath)
                    && string.Equals(item.LocalFilePath, removedPath, StringComparison.OrdinalIgnoreCase));
            }

            _currentCheckedVideoId = null;
            IsFavorite = false;
            _notifier.Success($"已删除「{current.Title}」的本地文件和收藏");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"删除失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TogglePlayAsync()
    {
        if (_playerService.IsPlaying)
        {
            await _playerService.PauseAsync();
        }
        else
        {
            await _playerService.ResumeAsync();
        }
    }

    [RelayCommand]
    private async Task PlayNextTrackAsync()
    {
        await _playerService.PlayNextAsync();
    }

    [RelayCommand]
    private async Task PlayPreviousTrackAsync()
    {
        await _playerService.PlayPreviousAsync();
    }

    [RelayCommand]
    private void TogglePlaybackMode()
    {
        _playerService.TogglePlaybackMode();
        NotifyChanged();
    }

    [RelayCommand]
    private async Task PlayVideoAsync()
    {
        if (!_playerService.CanShowVideoToggle() || _playerService.IsSwitchingTrack)
        {
            return;
        }

        var currentVideo = _playerService.CurrentVideo;
        if (currentVideo == null)
        {
            return;
        }

        if (await _playerService.CanPlayLocalVideoForCurrentAsync())
        {
            await NavigateToVideoIfSwitchedAsync(await _playerService.PlayCurrentAsVideoAsync());
            return;
        }

        if (string.IsNullOrWhiteSpace(currentVideo.VideoId) || currentVideo.VideoId == "local")
        {
            _notifier.Warning("当前曲目没有可用的视频 ID，无法在线播放视频。");
            return;
        }

        if (!await _dialogHost.ConfirmRemoteVideoPlayAsync(currentVideo.Title))
        {
            return;
        }

        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            var playOk = await _playerService.PlayRemoteVideoForCurrentAsync();
            PlaybackDiagnostics.Log(
                $"PlayerVM.PlayVideo playOk={playOk} isVideo={_playerService.IsCurrentStreamVideo} hybrid={_playerService.IsUsingHybridWebVideo} url={PlaybackDiagnostics.DescribeUrl(_playerService.CurrentStreamUrl)}");
            if (!await NavigateToVideoIfSwitchedAsync(playOk))
            {
                PlaybackDiagnostics.LogError(
                    $"PlayerVM.PlayVideo navigate failed playOk={playOk} isVideo={_playerService.IsCurrentStreamVideo}");
                _notifier.Error("在线视频播放失败，视频可能不可用或网络异常。");
            }
        }
        finally
        {
            _globalState.HideLoading();
            NotifyChanged();
        }
    }

    [RelayCommand]
    private async Task PlayAudioAsync()
    {
        if (_playerService.IsSwitchingTrack)
        {
            return;
        }

        if (await _playerService.PlayCurrentAsAudioAsync())
        {
            _navigation.NavigateTo("/player/audio", replace: true);
        }
        else
        {
            _notifier.Warning("无法切换到音频播放。");
        }
    }

    private Task<bool> NavigateToVideoIfSwitchedAsync(bool switched)
    {
        if (!switched)
        {
            PlaybackDiagnostics.Log("NavigateToVideo skipped: play returned false");
            return Task.FromResult(false);
        }

        if (!_playerService.IsCurrentStreamVideo)
        {
            PlaybackDiagnostics.Log(
                $"NavigateToVideo skipped: IsCurrentStreamVideo=false hybrid={_playerService.IsUsingHybridWebVideo}");
            return Task.FromResult(false);
        }

        if (!(OperatingSystem.IsAndroid() && _playerService.UseNativeVideoPlayback))
        {
            PlaybackDiagnostics.Log("NavigateToVideo -> /player/video");
            _navigation.NavigateTo("/player/video");
        }

        return Task.FromResult(true);
    }

    public static string GetPlaybackModeIcon(PlaybackMode mode) => mode switch
    {
        PlaybackMode.Sequential => Icons.Material.Filled.Repeat,
        PlaybackMode.Random => Icons.Material.Filled.Shuffle,
        PlaybackMode.SingleLoop => Icons.Material.Filled.RepeatOne,
        _ => Icons.Material.Filled.Repeat
    };
}
