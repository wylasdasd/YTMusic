using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class AListRemoteDownloadManagerService : IAListRemoteDownloadManagerService
    {
        private const int MaxRetainedTasks = 100;
        private readonly AListUploadService _aListUploadService;
        private readonly ILocalMusicService _localMusicService;
        private readonly object _syncRoot = new();
        private readonly List<DownloadTaskInfo> _activeRemoteDownloads = new();

        public AListRemoteDownloadManagerService(AListUploadService aListUploadService, ILocalMusicService localMusicService)
        {
            _aListUploadService = aListUploadService;
            _localMusicService = localMusicService;
        }

        public IReadOnlyList<DownloadTaskInfo> ActiveRemoteDownloads
        {
            get
            {
                lock (_syncRoot)
                {
                    return _activeRemoteDownloads.ToArray();
                }
            }
        }

        public event Action? OnRemoteDownloadsChanged;

        public void StartRemoteDownload(AListDirectoryItem item)
        {
            DownloadTaskInfo taskInfo;

            lock (_syncRoot)
            {
                var hasActiveDuplicate = _activeRemoteDownloads.Any(download =>
                    string.Equals(download.TaskKey, item.Path, StringComparison.OrdinalIgnoreCase) &&
                    (download.Status == DownloadStatus.Pending || download.Status == DownloadStatus.Downloading));

                if (hasActiveDuplicate)
                {
                    return;
                }

                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = item.Path,
                    Kind = TransferKind.RemoteDownload,
                    Title = item.Name,
                    SourcePath = item.Path,
                    Status = DownloadStatus.Pending
                };

                _activeRemoteDownloads.Add(taskInfo);
                TrimHistory_NoLock();
            }

            NotifyChanged();
            _ = ExecuteRemoteDownloadAsync(taskInfo);
        }

        private async Task ExecuteRemoteDownloadAsync(DownloadTaskInfo taskInfo)
        {
            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }
            NotifyChanged();

            try
            {
                var progress = new Progress<double>(p =>
                {
                    bool shouldNotify;
                    lock (_syncRoot)
                    {
                        shouldNotify = Math.Abs(taskInfo.Progress - p) >= 0.005 || p >= 1.0;
                        taskInfo.Progress = p;
                    }

                    if (shouldNotify)
                    {
                        NotifyChanged();
                    }
                });

                var localFilePath = await _aListUploadService.DownloadFileAsync(taskInfo.SourcePath!, progress);

                await _localMusicService.AddDownloadedTrackAsync(new DownloadedTrack
                {
                    VideoId = $"alist:{taskInfo.SourcePath}",
                    Title = taskInfo.Title,
                    Author = "AList",
                    ThumbnailUrl = null,
                    LocalFilePath = localFilePath,
                    IsVideo = false,
                    DownloadedDate = DateTime.UtcNow,
                    HasUploaded = false,
                    UploadedDate = null,
                    UploadedRemotePath = null,
                    RemoteSourcePath = taskInfo.SourcePath
                });

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = localFilePath;
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
                    TrimHistory_NoLock();
                }

                NotifyChanged();
            }
        }

        private void NotifyChanged()
        {
            OnRemoteDownloadsChanged?.Invoke();
        }

        private void TrimHistory_NoLock()
        {
            if (_activeRemoteDownloads.Count <= MaxRetainedTasks)
            {
                return;
            }

            var overflow = _activeRemoteDownloads.Count - MaxRetainedTasks;
            var removable = _activeRemoteDownloads
                .Where(task => task.Status is DownloadStatus.Completed or DownloadStatus.Failed)
                .OrderBy(task => task.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in removable)
            {
                _activeRemoteDownloads.Remove(item);
            }

            if (_activeRemoteDownloads.Count <= MaxRetainedTasks)
            {
                return;
            }

            overflow = _activeRemoteDownloads.Count - MaxRetainedTasks;
            var oldest = _activeRemoteDownloads
                .OrderBy(task => task.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in oldest)
            {
                _activeRemoteDownloads.Remove(item);
            }
        }
    }
}
