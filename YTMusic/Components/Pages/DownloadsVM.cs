using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.ApplicationModel;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class DownloadsVM : ViewModelBase, IDisposable
{
    public enum DownloadTaskFilter
    {
        All,
        Active,
        Completed,
        Failed
    }

    private readonly ILocalMusicService _localMusicService;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IAListRemoteDownloadManagerService _remoteDownloadManager;
    private readonly IUiNotifier _notifier;
    private readonly IFavoriteService _favoriteService;
    private readonly IDialogHost _dialogHost;
    private readonly MusicPlayerService _playerService;
    private readonly LocalFileDeleteCoordinator _localFileDelete;
    private readonly NavigationManager _navigation;
    private readonly DownloadRecordsSync _downloadRecordsSync;

    public List<DownloadedTrack> DownloadRecords { get; private set; } = new();
    public HashSet<string> FavoriteFilePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private int _selectedTabIndex = 1;

    [ObservableProperty]
    private int _downloadTasksRevision;

    public DownloadTaskFilter TaskFilter { get; private set; } = DownloadTaskFilter.All;

    public IReadOnlyList<DownloadTaskInfo> DownloadTasks =>
        _downloadManager.ActiveDownloads
            .Concat(_remoteDownloadManager.ActiveRemoteDownloads)
            .OrderBy(task => task.CreatedAtUtc)
            .ToArray();

    public IEnumerable<DownloadTaskInfo> FilteredDownloadTasks =>
        DownloadTasks.Where(task => TaskFilter switch
        {
            DownloadTaskFilter.Active => task.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused,
            DownloadTaskFilter.Completed => task.Status == DownloadStatus.Completed,
            DownloadTaskFilter.Failed => task.Status == DownloadStatus.Failed,
            _ => true
        });

    public DownloadsVM(
        ILocalMusicService localMusicService,
        IDownloadManagerService downloadManager,
        IAListRemoteDownloadManagerService remoteDownloadManager,
        IUiNotifier notifier,
        IFavoriteService favoriteService,
        IDialogHost dialogHost,
        MusicPlayerService playerService,
        LocalFileDeleteCoordinator localFileDelete,
        NavigationManager navigation,
        DownloadRecordsSync downloadRecordsSync)
    {
        _localMusicService = localMusicService;
        _downloadManager = downloadManager;
        _remoteDownloadManager = remoteDownloadManager;
        _notifier = notifier;
        _favoriteService = favoriteService;
        _dialogHost = dialogHost;
        _playerService = playerService;
        _localFileDelete = localFileDelete;
        _navigation = navigation;
        _downloadRecordsSync = downloadRecordsSync;
        _downloadManager.OnDownloadsChanged += HandleDownloadsChanged;
        _remoteDownloadManager.OnRemoteDownloadsChanged += HandleDownloadsChanged;
        _downloadRecordsSync.RecordsShouldRefresh += HandleRecordsShouldRefresh;
    }

    public void ApplyRouteTab(string? tab)
    {
        if (string.Equals(tab, "tasks", StringComparison.OrdinalIgnoreCase))
        {
            SelectedTabIndex = 0;
        }
        else if (string.Equals(tab, "records", StringComparison.OrdinalIgnoreCase))
        {
            SelectedTabIndex = 1;
        }
    }

    public void SetTaskFilter(DownloadTaskFilter filter)
    {
        if (TaskFilter == filter)
        {
            return;
        }

        TaskFilter = filter;
        NotifyChanged();
    }

    public string? GetTasksTabBadgeData()
    {
        var activeCount = DownloadTasks.Count(task =>
            task.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused);
        return activeCount > 0 ? $"{activeCount}" : null;
    }

    public static MudBlazor.Color GetTaskStatusColor(DownloadStatus status) => status switch
    {
        DownloadStatus.Pending => MudBlazor.Color.Default,
        DownloadStatus.Downloading => MudBlazor.Color.Info,
        DownloadStatus.Paused => MudBlazor.Color.Warning,
        DownloadStatus.Completed => MudBlazor.Color.Success,
        DownloadStatus.Failed => MudBlazor.Color.Error,
        DownloadStatus.Cancelled => MudBlazor.Color.Default,
        _ => MudBlazor.Color.Default
    };

    public static string GetTaskEmptyMessage(DownloadTaskFilter filter) => filter switch
    {
        DownloadTaskFilter.Active => "没有进行中的下载。",
        DownloadTaskFilter.Completed => "暂无已完成的任务。",
        DownloadTaskFilter.Failed => "暂无失败的下载。",
        _ => "暂无下载任务。"
    };

    public static string GetDownloadTaskLabel(DownloadTaskInfo task)
    {
        var status = task.Status switch
        {
            DownloadStatus.Pending => "排队中",
            DownloadStatus.Downloading => "下载中",
            DownloadStatus.Paused => "已暂停",
            DownloadStatus.Completed => "成功",
            DownloadStatus.Failed => "失败",
            DownloadStatus.Cancelled => "已取消",
            _ => task.Status.ToString()
        };

        if (task.Kind == TransferKind.RemoteDownload)
        {
            return $"AList · {status}";
        }

        return $"YouTube 音频 · {status}";
    }

    public static bool CanRedownloadFromYouTube(DownloadedTrack file)
    {
        var videoId = file.VideoId;
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return false;
        }

        if (videoId.Equals("local", StringComparison.OrdinalIgnoreCase)
            || LocalAudioFolder.IsImportedVideoId(videoId)
            || videoId.StartsWith(YTMusic.BLL.AppGlobal.AList.RemoteTrackIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public void CancelTask(DownloadTaskInfo task)
    {
        if (task.Kind == TransferKind.RemoteDownload)
        {
            _remoteDownloadManager.CancelRemoteDownload(task.Id);
        }
        else
        {
            _downloadManager.CancelDownload(task.Id);
        }
    }

    public void PauseTask(DownloadTaskInfo task)
    {
        if (task.Kind == TransferKind.RemoteDownload)
        {
            _remoteDownloadManager.PauseRemoteDownload(task.Id);
        }
        else
        {
            _downloadManager.PauseDownload(task.Id);
        }
    }

    public void ResumeTask(DownloadTaskInfo task)
    {
        if (task.Kind == TransferKind.RemoteDownload)
        {
            _remoteDownloadManager.ResumeRemoteDownload(task.Id);
        }
        else
        {
            _downloadManager.ResumeDownload(task.Id);
        }
    }

    public static bool CanPauseOrResume(DownloadTaskInfo task)
        => task.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused;

    public static bool CanCancel(DownloadTaskInfo task)
        => task.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused;

    public void RedownloadAsync(DownloadedTrack file)
    {
        if (!CanRedownloadFromYouTube(file))
        {
            _notifier.Warning("仅支持重新下载来自 YouTube 的曲目。");
            return;
        }

        var inProgress = _downloadManager.ActiveDownloads.Any(task =>
            task.VideoId == file.VideoId
            && !task.IsVideo
            && task.Status is DownloadStatus.Pending or DownloadStatus.Downloading);
        if (inProgress)
        {
            _notifier.Info("该曲目已在下载队列中。");
            return;
        }

        _downloadManager.StartDownload(file.VideoId, file.Title, isVideo: false);
        SelectedTabIndex = 0;
        SetTaskFilter(DownloadTaskFilter.Active);
        _notifier.Info($"已将「{file.Title}」加入下载队列");
        NotifyChanged();
    }

    private void HandleDownloadsChanged()
    {
        void Apply()
        {
            DownloadTasksRevision++;
            NotifyChanged();
        }

        if (MainThread.IsMainThread)
        {
            Apply();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(Apply);
        }
    }

    private void HandleRecordsShouldRefresh()
    {
        if (MainThread.IsMainThread)
        {
            _ = LoadDownloadRecordsAsync(showLoadingSpinner: false);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => _ = LoadDownloadRecordsAsync(showLoadingSpinner: false));
        }
    }

    public static bool HasLocalFile(DownloadedTrack file)
        => !string.IsNullOrWhiteSpace(file.LocalFilePath) && File.Exists(file.LocalFilePath);

    public void Dispose()
    {
        _downloadManager.OnDownloadsChanged -= HandleDownloadsChanged;
        _remoteDownloadManager.OnRemoteDownloadsChanged -= HandleDownloadsChanged;
        _downloadRecordsSync.RecordsShouldRefresh -= HandleRecordsShouldRefresh;
    }

    public Task LoadDownloadRecordsAsync(bool showLoadingSpinner = true)
    {
        return LoadDownloadRecordsCoreAsync(showLoadingSpinner);
    }

    private async Task LoadDownloadRecordsCoreAsync(bool showLoadingSpinner)
    {
        if (showLoadingSpinner)
        {
            IsLoading = true;
            NotifyChanged();
        }

        try
        {
            DownloadRecords = (await _localMusicService.GetDownloadRecordsAsync()).ToList();
            FavoriteFilePaths.Clear();

            var filePaths = DownloadRecords
                .Select(file => file.LocalFilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var favoritedPaths = await _favoriteService.GetFavoritedFilePathsAsync(filePaths);

            foreach (var path in favoritedPaths)
            {
                FavoriteFilePaths.Add(path);
            }
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to load download records: {ex.Message}");
        }
        finally
        {
            if (showLoadingSpinner)
            {
                IsLoading = false;
            }

            NotifyChanged();
        }
    }

    public async Task DeleteFileAsync(DownloadedTrack file)
    {
        try
        {
            FavoriteFilePaths.Remove(file.LocalFilePath);
            await _localFileDelete.RemoveDownloadedTrackAsync(
                _localMusicService,
                _favoriteService,
                file.VideoId,
                file.LocalFilePath);
            DownloadRecords.RemoveAll(item =>
                string.Equals(item.VideoId, file.VideoId, StringComparison.Ordinal));
            _notifier.Success($"已删除「{file.Title}」");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to delete: {ex.Message}");
        }
    }

    public async Task OpenFavoriteDialogAsync(DownloadedTrack file)
    {
        try
        {
            await _dialogHost.ShowFavoriteFolderDialogAsync(
                new FavoritePickRequest
                {
                    VideoId = file.VideoId,
                    Title = file.Title,
                    Author = file.Author,
                    ThumbnailUrl = file.ThumbnailUrl,
                    LocalFilePath = file.LocalFilePath
                },
                "Add to Favorite Folder");

            var stillFavorited = await _favoriteService.GetTrackByFilePathAsync(file.LocalFilePath);
            if (stillFavorited != null)
            {
                FavoriteFilePaths.Add(file.LocalFilePath);
            }
            else
            {
                FavoriteFilePaths.Remove(file.LocalFilePath);
            }

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to open favorites: {ex.Message}");
        }
    }

    public async Task PlayLocalAudioFileAsync(DownloadedTrack file)
    {
        if (!HasLocalFile(file))
        {
            _notifier.Warning("本地文件不存在，可尝试重新下载。");
            return;
        }

        if (await _playerService.PlayLocalFileAsync(file.LocalFilePath, file.Title, false, file.Author, file.ThumbnailUrl, file.VideoId))
        {
            _navigation.NavigateTo("/player");
        }
    }

    public async Task PlayLocalVideoFileAsync(DownloadedTrack file)
    {
        if (!HasLocalFile(file))
        {
            _notifier.Warning("本地文件不存在，可尝试重新下载。");
            return;
        }

        if (await _playerService.PlayLocalFileAsync(file.LocalFilePath, file.Title, true, file.Author, file.ThumbnailUrl, file.VideoId))
        {
            _navigation.NavigateTo("/player");
        }
    }
}
