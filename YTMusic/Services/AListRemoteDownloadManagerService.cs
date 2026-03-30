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
                var resolvedTitle = await ResolveDirectoryTitleAsync(directoryItem.Path, title);
                var children = await _aListUploadService.ListDirectoryItemsAsync(directoryItem.Path);
                var mediaItem = SelectMediaItem(children);
                if (mediaItem == null)
                {
                    throw new InvalidOperationException("No audio or video file was found in this AList directory.");
                }

                var coverItem = SelectCoverItem(children);
                var localDirectory = StoragePaths.GetDownloadedMusicDirectory();
                FileHelp.EnsureDirectoryExists(localDirectory);

                if (IsLikelyMd5(resolvedTitle))
                {
                    resolvedTitle = Path.GetFileNameWithoutExtension(mediaItem.Name);
                }

                var mediaFileName = BuildLocalMediaFileName(resolvedTitle, mediaItem.Name);
                var localMediaPath = Path.Combine(localDirectory, FileHelp.SafeFileName(mediaFileName));

                var mediaProgress = new Progress<double>(p => UpdateProgress(taskInfo, coverItem != null, p, 0));
                await _aListUploadService.DownloadFileToPathAsync(mediaItem.Path, localMediaPath, mediaProgress);

                string? localCoverPath = null;
                if (coverItem != null)
                {
                    var coverFileName = BuildLocalCoverFileName(localMediaPath, coverItem.Name);
                    localCoverPath = Path.Combine(localDirectory, FileHelp.SafeFileName(coverFileName));
                    var coverProgress = new Progress<double>(p => UpdateProgress(taskInfo, true, 1.0, p));
                    await _aListUploadService.DownloadFileToPathAsync(coverItem.Path, localCoverPath, coverProgress);
                }

                await _localMusicService.AddDownloadedTrackAsync(new DownloadedTrack
                {
                    VideoId = $"alist:{mediaItem.Path}",
                    Title = resolvedTitle,
                    Author = "AList",
                    ThumbnailUrl = string.IsNullOrWhiteSpace(localCoverPath) ? null : BuildCoverDataUrl(localCoverPath),
                    LocalFilePath = localMediaPath,
                    IsVideo = IsVideoFile(mediaItem.Name),
                    DownloadedDate = DateTime.UtcNow,
                    HasUploaded = false,
                    UploadedDate = null,
                    UploadedRemotePath = null,
                    RemoteSourcePath = mediaItem.Path
                });

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

        private void UpdateProgress(DownloadTaskInfo taskInfo, bool hasCover, double mediaProgress, double coverProgress)
        {
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
                NotifyChanged();
            }
        }

        private static AListDirectoryItem? SelectMediaItem(IReadOnlyList<AListDirectoryItem> items)
        {
            return items
                .Where(item => !item.IsDir && IsMediaFile(item.Name))
                .OrderByDescending(item => item.Size)
                .FirstOrDefault();
        }

        private static AListDirectoryItem? SelectCoverItem(IReadOnlyList<AListDirectoryItem> items)
        {
            return items
                .Where(item => !item.IsDir && IsImageFile(item.Name))
                .OrderBy(item => item.Name.StartsWith("cover", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(item => item.Size)
                .FirstOrDefault();
        }

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

        private static bool IsImageFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
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

        private static string BuildLocalCoverFileName(string localMediaPath, string remoteCoverName)
        {
            var mediaBaseName = Path.GetFileNameWithoutExtension(localMediaPath);
            var extension = Path.GetExtension(remoteCoverName);
            return $"{mediaBaseName}-cover{extension}";
        }

        private static string BuildCoverDataUrl(string localCoverPath)
        {
            var mimeType = GetImageMimeType(localCoverPath);
            var bytes = File.ReadAllBytes(localCoverPath);
            return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        }

        private static string GetImageMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                _ => "image/jpeg"
            };
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
