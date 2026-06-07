using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommonTool.FileHelps;

namespace YTMusic.Services
{
    public class UploadManagerService : IUploadManagerService
    {
        private const int MaxRetainedTasks = 100;
        private const double MaxInProgressUpload = 0.99;
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
                var directoryKey = BuildMusicDirectoryKey(track.Title);
                var remoteDirectory = AListUploadService.BuildRemotePath(_settingsService.RemoteDirectory, directoryKey);
                await _aListUploadService.CreateDirectoryAsync(remoteDirectory);

                var mediaExtension = Path.GetExtension(track.LocalFilePath);
                var mediaFileName = $"{directoryKey}{mediaExtension}";
                var remoteMediaPath = AListUploadService.BuildRemotePath(remoteDirectory, mediaFileName);
                // metadata.json = DownloadedTracks 子集（见 RemoteTrackMetadata）；先传 metadata，再传音视频。
                var metadata = RemoteTrackMetadata.FromDownloadedTrack(track);
                var remoteMetadataPath = AListUploadService.BuildRemotePath(remoteDirectory, RemoteTrackMetadata.FileName);
                var metadataProgress = new Progress<double>(p =>
                {
                    UpdateCombinedProgress(taskInfo, 0, p);
                });
                await _aListUploadService.UploadJsonToPathAsync(metadata, remoteMetadataPath, metadataProgress);

                var mediaProgress = new Progress<double>(p =>
                {
                    UpdateCombinedProgress(taskInfo, p, 1.0);
                });
                await _aListUploadService.UploadFileToPathAsync(track.LocalFilePath, remoteMediaPath, mediaProgress);

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = remoteDirectory;
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                    taskInfo.ErrorMessage = null;
                }
                NotifyUploadsChanged();

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
                    UpdateSingleFileProgress(taskInfo, p);
                });

                var remotePath = await _aListUploadService.UploadFileAsync(taskInfo.SourcePath!, taskInfo.Title, progress);

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = remotePath;
                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }
                NotifyUploadsChanged();

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

        private void UpdateCombinedProgress(DownloadTaskInfo taskInfo, double mediaProgress, double metadataProgress)
        {
            // metadata.json 10%（先传）+ 音视频 90%（后传）；未完成前最高 99%，确认成功后才设 100%。
            var combined = (metadataProgress * 0.1) + (mediaProgress * 0.9);
            ApplyInProgressUpload(taskInfo, combined);
        }

        private void UpdateSingleFileProgress(DownloadTaskInfo taskInfo, double progress)
        {
            ApplyInProgressUpload(taskInfo, progress);
        }

        private void ApplyInProgressUpload(DownloadTaskInfo taskInfo, double progress)
        {
            var capped = Math.Min(MaxInProgressUpload, progress);

            bool shouldNotify;
            lock (_syncRoot)
            {
                shouldNotify = Math.Abs(taskInfo.Progress - capped) >= 0.005;
                taskInfo.Progress = capped;
            }

            if (shouldNotify)
            {
                NotifyUploadsChanged();
            }
        }

        private static string BuildMusicDirectory(string baseDirectory, string title)
        {
            return AListUploadService.BuildRemotePath(baseDirectory, BuildMusicDirectoryKey(title));
        }

        private static string BuildMusicDirectoryKey(string title)
        {
            var source = string.IsNullOrWhiteSpace(title) ? "unknown" : title.Trim();
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(source);
            var hashBytes = md5.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
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
