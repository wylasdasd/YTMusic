using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonHelp;

using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Infrastructure;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Services
{
    public class DownloadManagerService : IDownloadManagerService
    {
        private const int MaxRetainedTasks = AppGlobal.Transfers.MaxRetainedTasks;
        private readonly IYouTubeService _youTubeService;
        private readonly ILocalMusicService _localMusicService;
        private readonly INetworkErrorService _networkErrorService;
        private readonly object _syncRoot = new();
        private readonly List<DownloadTaskInfo> _activeDownloads = new();
        private readonly Dictionary<string, CancellationTokenSource> _taskCancellation = new(StringComparer.Ordinal);

        public IReadOnlyList<DownloadTaskInfo> ActiveDownloads
        {
            get
            {
                lock (_syncRoot)
                {
                    return _activeDownloads.ToArray();
                }
            }
        }
        public event Action? OnDownloadsChanged;

        public DownloadManagerService(IYouTubeService youTubeService, ILocalMusicService localMusicService, INetworkErrorService networkErrorService)
        {
            _youTubeService = youTubeService;
            _localMusicService = localMusicService;
            _networkErrorService = networkErrorService;
        }

        public void StartDownload(string videoId, string title, bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return;
            }

            DownloadTaskInfo taskInfo;

            lock (_syncRoot)
            {
                bool hasActiveDuplicate = _activeDownloads.Any(d =>
                    d.VideoId == videoId &&
                    d.IsVideo == isVideo &&
                    d.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused);

                if (hasActiveDuplicate)
                {
                    return;
                }

                var staleTasks = _activeDownloads
                    .Where(d =>
                        d.VideoId == videoId
                        && d.IsVideo == isVideo
                        && d.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                    .ToList();
                foreach (var stale in staleTasks)
                {
                    _activeDownloads.Remove(stale);
                    DisposeTaskCancellation(stale.Id);
                }

                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = $"{videoId}:{isVideo}",
                    Kind = TransferKind.Download,
                    VideoId = videoId,
                    Title = title,
                    IsVideo = isVideo,
                    Status = DownloadStatus.Pending,
                    Progress = 0
                };

                _activeDownloads.Add(taskInfo);
                _taskCancellation[taskInfo.Id] = new CancellationTokenSource();
                TrimHistory_NoLock();
            }

            NotifyDownloadsChanged();
            _ = ExecuteDownloadAsync(taskInfo);
        }

        public void CancelDownload(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            lock (_syncRoot)
            {
                var task = _activeDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null)
                {
                    return;
                }

                if (task.Status is DownloadStatus.Pending or DownloadStatus.Paused)
                {
                    task.Status = DownloadStatus.Cancelled;
                    task.ErrorMessage = "已取消";
                    DisposeTaskCancellation(taskId);
                    NotifyDownloadsChanged();
                    return;
                }

                if (task.Status == DownloadStatus.Downloading && _taskCancellation.TryGetValue(taskId, out var cts))
                {
                    cts.Cancel();
                }
            }
        }

        public void PauseDownload(string taskId)
        {
            lock (_syncRoot)
            {
                var task = _activeDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null || task.Status is not (DownloadStatus.Downloading or DownloadStatus.Pending))
                {
                    return;
                }

                task.Status = DownloadStatus.Paused;
            }

            NotifyDownloadsChanged();
        }

        public void ResumeDownload(string taskId)
        {
            lock (_syncRoot)
            {
                var task = _activeDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null || task.Status != DownloadStatus.Paused)
                {
                    return;
                }

                task.Status = DownloadStatus.Downloading;
            }

            NotifyDownloadsChanged();
        }

        private async Task ExecuteDownloadAsync(DownloadTaskInfo taskInfo)
        {
            CancellationToken cancellationToken;
            lock (_syncRoot)
            {
                if (!_taskCancellation.TryGetValue(taskInfo.Id, out var cts))
                {
                    cts = new CancellationTokenSource();
                    _taskCancellation[taskInfo.Id] = cts;
                }

                cancellationToken = cts.Token;
            }

            await WaitUntilCanStartAsync(taskInfo, cancellationToken).ConfigureAwait(false);
            if (taskInfo.Status == DownloadStatus.Cancelled)
            {
                return;
            }

            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
                taskInfo.Progress = 0;
            }

            NotifyDownloadsChanged();

            string? targetPath = null;
            try
            {
                targetPath = await _youTubeService.ResolveDownloadTargetPathAsync(
                    taskInfo.VideoId,
                    taskInfo.Title,
                    taskInfo.IsVideo).ConfigureAwait(false);
                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = targetPath;
                }

                NotifyDownloadsChanged();

                var progress = DownloadTransferControl.CreateProgressReporter(
                    taskInfo,
                    _syncRoot,
                    cancellationToken,
                    AppGlobal.Transfers.ProgressNotifyThreshold,
                    NotifyDownloadsChanged);

                string filePath = await _youTubeService.DownloadAsync(
                    taskInfo.VideoId,
                    taskInfo.Title,
                    taskInfo.IsVideo,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                await _localMusicService.SaveCompletedDownloadRecordAsync(
                    taskInfo.VideoId,
                    taskInfo.Title,
                    filePath,
                    taskInfo.IsVideo).ConfigureAwait(false);

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = filePath;
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }
            }
            catch (OperationCanceledException)
            {
                FileHelp.DeleteIfExists(targetPath);
                lock (_syncRoot)
                {
                    taskInfo.Status = DownloadStatus.Cancelled;
                    taskInfo.ErrorMessage = "已取消";
                }
            }
            catch (Exception ex)
            {
                FileHelp.DeleteIfExists(targetPath);
                lock (_syncRoot)
                {
                    taskInfo.Status = DownloadStatus.Failed;
                    taskInfo.ErrorMessage = ex.Message;
                }

                if (NetworkErrorHelper.IsNetworkRelated(ex))
                {
                    _ = _networkErrorService.NotifyFailureAsync("下载", ex);
                }
            }
            finally
            {
                DisposeTaskCancellation(taskInfo.Id);
                lock (_syncRoot)
                {
                    TrimHistory_NoLock();
                }

                NotifyDownloadsChanged();
            }
        }

        private async Task WaitUntilCanStartAsync(DownloadTaskInfo taskInfo, CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_syncRoot)
                {
                    if (taskInfo.Status == DownloadStatus.Cancelled)
                    {
                        return;
                    }

                    if (taskInfo.Status != DownloadStatus.Paused)
                    {
                        return;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(150, cancellationToken);
            }
        }

        private void DisposeTaskCancellation(string taskId)
        {
            if (_taskCancellation.Remove(taskId, out var cts))
            {
                cts.Dispose();
            }
        }

        private void NotifyDownloadsChanged()
        {
            OnDownloadsChanged?.Invoke();
        }

        private void TrimHistory_NoLock()
        {
            if (_activeDownloads.Count <= MaxRetainedTasks)
            {
                return;
            }

            int overflow = _activeDownloads.Count - MaxRetainedTasks;

            var removable = _activeDownloads
                .Where(d => d.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                .OrderBy(d => d.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in removable)
            {
                _activeDownloads.Remove(item);
                DisposeTaskCancellation(item.Id);
            }

            if (_activeDownloads.Count <= MaxRetainedTasks)
            {
                return;
            }

            overflow = _activeDownloads.Count - MaxRetainedTasks;
            var oldest = _activeDownloads
                .OrderBy(d => d.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in oldest)
            {
                _activeDownloads.Remove(item);
                DisposeTaskCancellation(item.Id);
            }
        }
    }
}
