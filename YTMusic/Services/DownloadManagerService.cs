using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class DownloadManagerService : IDownloadManagerService
    {
        // 防止 Transfers 页面无限增长，保留最近 N 条任务记录。
        private const int MaxRetainedTasks = 100;
        private readonly IYouTubeService _youTubeService;
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly object _syncRoot = new();
        private readonly List<DownloadTaskInfo> _activeDownloads = new();

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

        public DownloadManagerService(IYouTubeService youTubeService, IFavoriteService favoriteService, ILocalMusicService localMusicService)
        {
            _youTubeService = youTubeService;
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
        }

        public void StartDownload(string videoId, string title, bool isVideo)
        {
            DownloadTaskInfo taskInfo;

            lock (_syncRoot)
            {
                // 同一个资源（videoId + 类型）正在排队/下载时不重复入队。
                bool hasActiveDuplicate = _activeDownloads.Any(d =>
                    d.VideoId == videoId &&
                    d.IsVideo == isVideo &&
                    (d.Status == DownloadStatus.Pending || d.Status == DownloadStatus.Downloading));

                if (hasActiveDuplicate)
                {
                    return;
                }

                taskInfo = new DownloadTaskInfo
                {
                    VideoId = videoId,
                    Title = title,
                    IsVideo = isVideo,
                    Status = DownloadStatus.Pending
                };

                _activeDownloads.Add(taskInfo);
                TrimHistory_NoLock();
            }

            NotifyDownloadsChanged();
            _ = ExecuteDownloadAsync(taskInfo);
        }

        private async Task ExecuteDownloadAsync(DownloadTaskInfo taskInfo)
        {
            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }
            NotifyDownloadsChanged();

            try
            {
                // 进度回调由下载流触发，更新共享任务状态时加锁保证一致性。
                var progress = new Progress<double>(p =>
                {
                    bool shouldNotify;
                    lock (_syncRoot)
                    {
                        // 仅在进度有明显变化时刷新 UI，降低高频重绘开销。
                        shouldNotify = Math.Abs(taskInfo.Progress - p) >= 0.005 || p >= 1.0;
                        taskInfo.Progress = p;
                    }
                    if (shouldNotify)
                    {
                        NotifyDownloadsChanged();
                    }
                });

                string filePath = await _youTubeService.DownloadAsync(taskInfo.VideoId, taskInfo.Title, taskInfo.IsVideo, progress);

                // Add to download history database.
                await _localMusicService.AddDownloadedTrackAsync(new DownloadedTrack
                {
                    VideoId = taskInfo.VideoId,
                    Title = taskInfo.Title,
                    Author = "Unknown Artist",
                    ThumbnailUrl = $"https://img.youtube.com/vi/{taskInfo.VideoId}/mqdefault.jpg",
                    LocalFilePath = filePath,
                    DownloadedDate = DateTime.UtcNow
                });

                // Update favorite local path if present.
                await _favoriteService.UpdateLocalFilePathAsync(taskInfo.VideoId, filePath);

                lock (_syncRoot)
                {
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }
            }
            catch (Exception ex)
            {
                lock (_syncRoot)
                {
                    taskInfo.Status = DownloadStatus.Failed;
                    taskInfo.ErrorMessage = ex.Message;
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    // 每次任务结束后做一次历史裁剪，避免长期运行内存增长。
                    TrimHistory_NoLock();
                }
                NotifyDownloadsChanged();
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

            // Prefer trimming completed/failed items first.
            var removable = _activeDownloads
                .Where(d => d.Status is DownloadStatus.Completed or DownloadStatus.Failed)
                .OrderBy(d => d.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in removable)
            {
                _activeDownloads.Remove(item);
            }

            if (_activeDownloads.Count <= MaxRetainedTasks)
            {
                return;
            }

            overflow = _activeDownloads.Count - MaxRetainedTasks;
            // 如果已完成任务不足以裁剪，再按最老时间兜底裁剪。
            var oldest = _activeDownloads
                .OrderBy(d => d.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in oldest)
            {
                _activeDownloads.Remove(item);
            }
        }
    }
}
