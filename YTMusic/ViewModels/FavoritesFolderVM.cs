using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;

namespace YTMusic.ViewModels;

public enum FavoriteTrackSortMode
{
    Time = 0,
    Name = 1,
    Downloaded = 2,
    NotDownloaded = 3
}

public sealed class FavoritesFolderVM : ViewModelBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ILocalMusicService _localMusicService;
    private readonly IUiNotifier _notifier;
    private readonly IDialogHost _dialogHost;
    private List<FavoriteTrack> _rawTracks = new();

    public int FolderId { get; private set; }
    public string? FolderName { get; private set; }
    public List<FavoriteTrack> Tracks { get; private set; } = new();
    public HashSet<string> FavoritedVideoIds { get; private set; } = new(StringComparer.Ordinal);
    public FavoriteTrackSortMode SortMode { get; set; } = FavoriteTrackSortMode.Time;
    public bool IsLoading { get; private set; }

    public bool IsDownloadedCatalog => FavoriteFolderIds.IsDownloadedCatalog(FolderId);

    public FavoritesFolderVM(
        IFavoriteService favoriteService,
        ILocalMusicService localMusicService,
        IUiNotifier notifier,
        IDialogHost dialogHost)
    {
        _favoriteService = favoriteService;
        _localMusicService = localMusicService;
        _notifier = notifier;
        _dialogHost = dialogHost;
    }

    public async Task InitializeAsync(int folderId)
    {
        FolderId = folderId;
        SortMode = FavoriteTrackSortMode.Time;

        var folders = await _favoriteService.GetFoldersAsync();
        FolderName = folders.FirstOrDefault(f => f.Id == folderId)?.Name;

        if (FolderName == null && !FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            _notifier.Warning("收藏夹不存在。");
            return;
        }

        if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
        {
            FolderName = folders.FirstOrDefault(f => f.Id == folderId)?.Name ?? "已下载";
        }

        await LoadTracksAsync();
    }

    public async Task LoadTracksAsync()
    {
        IsLoading = true;
        NotifyChanged();

        try
        {
            if (IsDownloadedCatalog)
            {
                _rawTracks = await LoadDownloadedCatalogTracksAsync();
            }
            else
            {
                _rawTracks = await _favoriteService.GetTracksAsync(FolderId, null);

                foreach (var track in _rawTracks)
                {
                    var downloadedTrack = await _localMusicService.GetDownloadedTrackByVideoIdAsync(track.VideoId);
                    if (downloadedTrack?.IsVideo == true)
                    {
                        track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
                    }
                }
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

    private void ApplyTrackOrdering()
    {
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

    public async Task DeleteLocalFileAsync(FavoriteTrack track)
    {
        if (!IsDownloadedCatalog || string.IsNullOrWhiteSpace(track.LocalFilePath))
        {
            return;
        }

        try
        {
            await _favoriteService.RemoveTrackByFilePathAsync(track.LocalFilePath);
            await _localMusicService.RemoveDownloadedTrackAsync(track.VideoId, track.LocalFilePath);
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
}
