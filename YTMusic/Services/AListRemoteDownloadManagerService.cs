using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonTool.FileHelps;

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
                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = Guid.NewGuid().ToString("N"),
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

        public void StartRemoteDirectoryDownload(AListDirectoryItem item, string? displayTitle = null)
        {
            if (item == null || !item.IsDir || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            DownloadTaskInfo taskInfo;
            var title = string.IsNullOrWhiteSpace(displayTitle) ? item.Name : displayTitle.Trim();

            lock (_syncRoot)
            {
                taskInfo = new DownloadTaskInfo
                {
                    TaskKey = Guid.NewGuid().ToString("N"),
                    Kind = TransferKind.RemoteDownload,
                    Title = title,
                    SourcePath = item.Path,
                    Status = DownloadStatus.Pending
                };

                _activeRemoteDownloads.Add(taskInfo);
                TrimHistory_NoLock();
            }

            NotifyChanged();
            _ = ExecuteRemoteDirectoryDownloadAsync(item, title, taskInfo);
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

        private async Task ExecuteRemoteDirectoryDownloadAsync(AListDirectoryItem directoryItem, string title, DownloadTaskInfo taskInfo)
        {
            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }
            NotifyChanged();

            try
            {
                var directoryKey = directoryItem.Name;
                var resolvedTitle = await ResolveDirectoryTitleAsync(directoryItem.Path, title);
                var children = await _aListUploadService.ListDirectoryItemsAsync(directoryItem.Path);
                var metadata = await TryLoadRemoteMetadataAsync(children, directoryItem.Path);
                var mediaItem = SelectMediaItem(children, directoryKey);
                if (mediaItem == null)
                {
                    throw new InvalidOperationException("No audio or video file was found in this AList directory.");
                }

                var localDirectory = StoragePaths.GetDownloadedMusicDirectory();
                FileHelp.EnsureDirectoryExists(localDirectory);

                var resolvedTitleFromMetadata = metadata?.Title;
                if (!string.IsNullOrWhiteSpace(resolvedTitleFromMetadata))
                {
                    resolvedTitle = resolvedTitleFromMetadata;
                }
                else if (IsLikelyMd5(resolvedTitle))
                {
                    resolvedTitle = Path.GetFileNameWithoutExtension(mediaItem.Name);
                }

                var mediaFileName = BuildLocalMediaFileName(resolvedTitle, mediaItem.Name);
                var localMediaPath = Path.Combine(localDirectory, FileHelp.SafeFileName(mediaFileName));

                var mediaProgress = new Progress<double>(p => UpdateProgress(taskInfo, p));
                await _aListUploadService.DownloadFileToPathAsync(mediaItem.Path, localMediaPath, mediaProgress);

                DownloadedTrack downloadedTrack;
                if (metadata != null)
                {
                    downloadedTrack = metadata.ToDownloadedTrack(localMediaPath, mediaItem.Path);
                    if (string.IsNullOrWhiteSpace(downloadedTrack.Title))
                    {
                        downloadedTrack.Title = resolvedTitle;
                    }
                }
                else
                {
                    downloadedTrack = new DownloadedTrack
                    {
                        VideoId = $"alist:{mediaItem.Path}",
                        Title = resolvedTitle,
                        Author = "AList",
                        LocalFilePath = localMediaPath,
                        DownloadedDate = DateTime.UtcNow,
                        RemoteSourcePath = mediaItem.Path
                    };
                }

                if (IsVideoFile(mediaItem.Name))
                {
                    downloadedTrack.IsVideo = true;
                }

                await _localMusicService.AddDownloadedTrackAsync(downloadedTrack);

                lock (_syncRoot)
                {
                    taskInfo.Title = resolvedTitle;
                    taskInfo.DestinationPath = localMediaPath;
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

        private async Task<string> ResolveDirectoryTitleAsync(string directoryPath, string currentTitle)
        {
            if (!IsLikelyMd5(currentTitle))
            {
                return currentTitle;
            }

            var normalizedDirectory = AListUploadSettingsService.NormalizeDirectory(directoryPath);
            var tracks = await _localMusicService.GetDownloadedTracksAsync();

            var uploadedTrack = tracks.FirstOrDefault(track =>
                !string.IsNullOrWhiteSpace(track.UploadedRemotePath) &&
                string.Equals(track.UploadedRemotePath, normalizedDirectory, StringComparison.OrdinalIgnoreCase) &&
                !IsLikelyMd5(track.Title));
            if (uploadedTrack != null)
            {
                return uploadedTrack.Title;
            }

            var downloadedTrack = tracks.FirstOrDefault(track =>
                !string.IsNullOrWhiteSpace(track.RemoteSourcePath) &&
                track.RemoteSourcePath!.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase) &&
                !IsLikelyMd5(track.Title));
            if (downloadedTrack != null)
            {
                return downloadedTrack.Title;
            }

            return currentTitle;
        }

        private void NotifyChanged()
        {
            OnRemoteDownloadsChanged?.Invoke();
        }

        private void UpdateProgress(DownloadTaskInfo taskInfo, double mediaProgress)
        {
            bool shouldNotify;
            var combined = mediaProgress;
            lock (_syncRoot)
            {
                shouldNotify = Math.Abs(taskInfo.Progress - combined) >= 0.005 || combined >= 1.0;
                taskInfo.Progress = combined;
            }

            if (shouldNotify)
            {
                NotifyChanged();
            }
        }

        private async Task<RemoteTrackMetadata?> TryLoadRemoteMetadataAsync(IReadOnlyList<AListDirectoryItem> items, string directoryPath)
        {
            var metadataItem = items.FirstOrDefault(item =>
                !item.IsDir &&
                item.Name.Equals(RemoteTrackMetadata.FileName, StringComparison.OrdinalIgnoreCase));

            if (metadataItem == null)
            {
                var metadataPath = AListUploadService.BuildRemotePath(directoryPath, RemoteTrackMetadata.FileName);
                return await _aListUploadService.TryDownloadJsonAsync<RemoteTrackMetadata>(metadataPath);
            }

            return await _aListUploadService.TryDownloadJsonAsync<RemoteTrackMetadata>(metadataItem.Path);
        }

        private static AListDirectoryItem? SelectMediaItem(IReadOnlyList<AListDirectoryItem> items, string? directoryKey = null)
        {
            var mediaItems = items
                .Where(item => !item.IsDir && IsMediaFile(item.Name))
                .ToList();

            if (!string.IsNullOrWhiteSpace(directoryKey))
            {
                var preferred = mediaItems.FirstOrDefault(item =>
                    string.Equals(Path.GetFileNameWithoutExtension(item.Name), directoryKey, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                {
                    return preferred;
                }
            }

            return mediaItems
                .OrderByDescending(item => item.Size)
                .FirstOrDefault();
        }

        // 封面文件下载已停用：SelectCoverItem / IsImageFile / BuildLocalCoverFileName / BuildCoverDataUrl / GetImageMimeType

        private static bool IsMediaFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".opus", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVideoFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyMd5(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
            {
                return false;
            }

            return value.All(Uri.IsHexDigit);
        }

        private static string BuildLocalMediaFileName(string title, string remoteMediaName)
        {
            var extension = Path.GetExtension(remoteMediaName);
            var safeTitle = FileHelp.SafeFileName(string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(remoteMediaName) : title);
            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                safeTitle = Path.GetFileNameWithoutExtension(remoteMediaName);
            }

            return $"{safeTitle}{extension}";
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
