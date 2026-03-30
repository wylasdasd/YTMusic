using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class UploadManagerService : IUploadManagerService
    {
        private const int MaxRetainedTasks = 100;
        private readonly AListUploadService _aListUploadService;
        private readonly AListUploadSettingsService _settingsService;
        private readonly ILocalMusicService _localMusicService;
        private readonly object _syncRoot = new();
        private readonly List<DownloadTaskInfo> _activeUploads = new();

        public UploadManagerService(AListUploadService aListUploadService, AListUploadSettingsService settingsService, ILocalMusicService localMusicService)
        {
            _aListUploadService = aListUploadService;
            _settingsService = settingsService;
            _localMusicService = localMusicService;
        }

        public IReadOnlyList<DownloadTaskInfo> ActiveUploads
        {
            get
            {
                lock (_syncRoot)
                {
                    return _activeUploads.ToArray();
                }
            }
        }

        public event Action? OnUploadsChanged;

        public void StartUpload(string localFilePath, string displayName)
        {
            DownloadTaskInfo taskInfo;
            var taskKey = $"{localFilePath}|{_settingsService.RemoteDirectory}";

            lock (_syncRoot)
            {
                var hasActiveDuplicate = _activeUploads.Any(upload =>
                    string.Equals(upload.TaskKey, taskKey, StringComparison.OrdinalIgnoreCase) &&
                    (upload.Status == DownloadStatus.Pending || upload.Status == DownloadStatus.Downloading));

                if (hasActiveDuplicate)
                {
                    return;
                }

                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = taskKey,
                    Kind = TransferKind.Upload,
                    Title = displayName,
                    SourcePath = localFilePath,
                    Status = DownloadStatus.Pending
                };

                _activeUploads.Add(taskInfo);
                TrimHistory_NoLock();
            }

            NotifyUploadsChanged();
            _ = ExecuteUploadAsync(taskInfo);
        }

        private async Task ExecuteUploadAsync(DownloadTaskInfo taskInfo)
        {
            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }
            NotifyUploadsChanged();

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
                        NotifyUploadsChanged();
                    }
                });

                var remotePath = await _aListUploadService.UploadFileAsync(taskInfo.SourcePath!, taskInfo.Title, progress);

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = remotePath;
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }

                await _localMusicService.MarkTrackUploadedAsync(taskInfo.SourcePath!, remotePath);
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

                NotifyUploadsChanged();
            }
        }

        private void NotifyUploadsChanged()
        {
            OnUploadsChanged?.Invoke();
        }

        private void TrimHistory_NoLock()
        {
            if (_activeUploads.Count <= MaxRetainedTasks)
            {
                return;
            }

            var overflow = _activeUploads.Count - MaxRetainedTasks;
            var removable = _activeUploads
                .Where(task => task.Status is DownloadStatus.Completed or DownloadStatus.Failed)
                .OrderBy(task => task.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in removable)
            {
                _activeUploads.Remove(item);
            }

            if (_activeUploads.Count <= MaxRetainedTasks)
            {
                return;
            }

            overflow = _activeUploads.Count - MaxRetainedTasks;
            var oldest = _activeUploads
                .OrderBy(task => task.CreatedAtUtc)
                .Take(overflow)
                .ToList();

            foreach (var item in oldest)
            {
                _activeUploads.Remove(item);
            }
        }
    }
}
