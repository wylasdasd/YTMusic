using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonTool.FileHelps;

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

        public void StartUpload(DownloadedTrack track)
        {
            if (track == null || string.IsNullOrWhiteSpace(track.LocalFilePath))
            {
                return;
            }

            DownloadTaskInfo taskInfo;
            var remoteDirectory = BuildMusicDirectory(_settingsService.RemoteDirectory, track.Title);

            lock (_syncRoot)
            {
                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = Guid.NewGuid().ToString("N"),
                    Kind = TransferKind.Upload,
                    Title = track.Title,
                    SourcePath = track.LocalFilePath,
                    DestinationPath = remoteDirectory,
                    Status = DownloadStatus.Pending
                };

                _activeUploads.Add(taskInfo);
                TrimHistory_NoLock();
            }

            NotifyUploadsChanged();
            _ = ExecuteTrackUploadAsync(track, taskInfo);
        }

        public void StartUpload(string localFilePath, string displayName)
        {
            DownloadTaskInfo taskInfo;

            lock (_syncRoot)
            {
                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = Guid.NewGuid().ToString("N"),
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

        private async Task ExecuteTrackUploadAsync(DownloadedTrack track, DownloadTaskInfo taskInfo)
        {
            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }
            NotifyUploadsChanged();

            try
            {
                var remoteDirectory = BuildMusicDirectory(_settingsService.RemoteDirectory, track.Title);
                await _aListUploadService.CreateDirectoryAsync(remoteDirectory);

                var mediaFileName = Path.GetFileName(track.LocalFilePath);
                var remoteMediaPath = AListUploadService.BuildRemotePath(remoteDirectory, mediaFileName);

                var mediaProgress = new Progress<double>(p =>
                {
                    UpdateCombinedProgress(taskInfo, track.ThumbnailUrl, p, 0);
                });

                await _aListUploadService.UploadFileToPathAsync(track.LocalFilePath, remoteMediaPath, mediaProgress);

                if (!string.IsNullOrWhiteSpace(track.ThumbnailUrl))
                {
                    var coverFileName = BuildCoverFileName(track.ThumbnailUrl);
                    var remoteCoverPath = AListUploadService.BuildRemotePath(remoteDirectory, coverFileName);
                    var coverProgress = new Progress<double>(p =>
                    {
                        UpdateCombinedProgress(taskInfo, track.ThumbnailUrl, 1.0, p);
                    });

                    await _aListUploadService.UploadCoverAsync(track.ThumbnailUrl, remoteCoverPath, coverProgress);
                }

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = remoteDirectory;
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }

                await _localMusicService.MarkTrackUploadedAsync(track.LocalFilePath, remoteDirectory);
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

        private void UpdateCombinedProgress(DownloadTaskInfo taskInfo, string? thumbnailUrl, double mediaProgress, double coverProgress)
        {
            var hasCover = !string.IsNullOrWhiteSpace(thumbnailUrl);
            var combined = hasCover
                ? (mediaProgress * 0.9) + (coverProgress * 0.1)
                : mediaProgress;

            bool shouldNotify;
            lock (_syncRoot)
            {
                shouldNotify = Math.Abs(taskInfo.Progress - combined) >= 0.005 || combined >= 1.0;
                taskInfo.Progress = combined;
            }

            if (shouldNotify)
            {
                NotifyUploadsChanged();
            }
        }

        private static string BuildMusicDirectory(string baseDirectory, string title)
        {
            var source = string.IsNullOrWhiteSpace(title) ? "unknown" : title.Trim();
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(source);
            var hashBytes = md5.ComputeHash(bytes);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return AListUploadService.BuildRemotePath(baseDirectory, hash);
        }

        private static string BuildCoverFileName(string thumbnailUrl)
        {
            var extension = ".jpg";
            try
            {
                var uri = new Uri(thumbnailUrl, UriKind.Absolute);
                var candidate = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 5)
                {
                    extension = candidate;
                }
            }
            catch
            {
            }

            return $"cover{extension}";
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
