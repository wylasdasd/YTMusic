using MudBlazor;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;

namespace YTMusic.ViewModels;

public sealed class PlayerVM : ViewModelBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IDialogHost _dialogHost;
    private readonly IUiNotifier _notifier;

    public bool IsFavorite { get; private set; }
    private string? _currentCheckedVideoId;

    public PlayerVM(
        IFavoriteService favoriteService,
        IDownloadManagerService downloadManager,
        IDialogHost dialogHost,
        IUiNotifier notifier)
    {
        _favoriteService = favoriteService;
        _downloadManager = downloadManager;
        _dialogHost = dialogHost;
        _notifier = notifier;
    }

    public async Task RefreshFavoriteStatusAsync(PlayingItem? current)
    {
        if (current == null)
        {
            IsFavorite = false;
            _currentCheckedVideoId = null;
            NotifyChanged();
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

        NotifyChanged();
    }

    public async Task OpenFavoriteDialogAsync(PlayingItem current)
    {
        var videoId = current.VideoId;

        if (!string.IsNullOrEmpty(current.LocalFilePath))
        {
            var existingTrack = await _favoriteService.GetTrackByFilePathAsync(current.LocalFilePath);
            videoId = existingTrack?.VideoId ?? Guid.NewGuid().ToString("N")[..11];
        }

        await _dialogHost.ShowFavoriteFolderDialogAsync(
            new FavoritePickRequest
            {
                VideoId = videoId,
                Title = current.Title,
                Author = current.Author,
                ThumbnailUrl = current.ThumbnailUrl,
                LocalFilePath = current.LocalFilePath
            },
            "Add to Favorite Folder");

        _currentCheckedVideoId = null;
        await RefreshFavoriteStatusAsync(current);
    }

    public void Download(PlayingItem current, bool isVideo)
    {
        if (current.VideoId == "local")
        {
            _notifier.Warning("This file is already downloaded.");
            return;
        }

        _downloadManager.StartDownload(current.VideoId, current.Title, isVideo);
        _notifier.Success($"Started downloading: {current.Title}");
    }

    public Task<bool> ConfirmRemoteVideoPlayAsync(string trackTitle)
        => _dialogHost.ConfirmRemoteVideoPlayAsync(trackTitle);

    public static string GetPlaybackModeIcon(MusicPlayerService.PlaybackMode mode) => mode switch
    {
        MusicPlayerService.PlaybackMode.Sequential => Icons.Material.Filled.Repeat,
        MusicPlayerService.PlaybackMode.Random => Icons.Material.Filled.Shuffle,
        MusicPlayerService.PlaybackMode.SingleLoop => Icons.Material.Filled.RepeatOne,
        _ => Icons.Material.Filled.Repeat
    };
}
