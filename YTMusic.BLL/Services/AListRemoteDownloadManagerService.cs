using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonHelp;

using System.Threading;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Infrastructure;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services
{
    public class AListRemoteDownloadManagerService : IAListRemoteDownloadManagerService
    {
        private const int MaxRetainedTasks = AppGlobal.Transfers.MaxRetainedTasks;
        private readonly IAListUploadService _aListUploadService;
        private readonly ILocalMusicService _localMusicService;
        private readonly IFavoriteService _favoriteService;
        private readonly IDownloadMusicDirectoryProvider _downloadMusicDirectoryProvider;
        private readonly object _syncRoot = new();
        private readonly List<DownloadTaskInfo> _activeRemoteDownloads = new();
        private readonly Dictionary<string, CancellationTokenSource> _taskCancellation = new(StringComparer.Ordinal);

        public AListRemoteDownloadManagerService(
            IAListUploadService aListUploadService,
            ILocalMusicService localMusicService,
            IFavoriteService favoriteService,
            IDownloadMusicDirectoryProvider downloadMusicDirectoryProvider)
        {
            _aListUploadService = aListUploadService;
            _localMusicService = localMusicService;
            _favoriteService = favoriteService;
            _downloadMusicDirectoryProvider = downloadMusicDirectoryProvider;
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
                _taskCancellation[taskInfo.Id] = new CancellationTokenSource();
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
                _taskCancellation[taskInfo.Id] = new CancellationTokenSource();
                TrimHistory_NoLock();
            }

            NotifyChanged();
            _ = ExecuteRemoteDirectoryDownloadAsync(item, title, taskInfo);
        }

        public void CancelRemoteDownload(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            lock (_syncRoot)
            {
                var task = _activeRemoteDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null)
                {
                    return;
                }

                if (task.Status is DownloadStatus.Pending or DownloadStatus.Paused)
                {
                    task.Status = DownloadStatus.Cancelled;
                    task.ErrorMessage = "已取消";
                    DisposeTaskCancellation(taskId);
                    NotifyChanged();
                    return;
                }

                if (task.Status == DownloadStatus.Downloading && _taskCancellation.TryGetValue(taskId, out var cts))
                {
                    cts.Cancel();
                }
            }
        }

        public void PauseRemoteDownload(string taskId)
        {
            lock (_syncRoot)
            {
                var task = _activeRemoteDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null || task.Status is not (DownloadStatus.Downloading or DownloadStatus.Pending))
                {
                    return;
                }

                task.Status = DownloadStatus.Paused;
            }

            NotifyChanged();
        }

        public void ResumeRemoteDownload(string taskId)
        {
            lock (_syncRoot)
            {
                var task = _activeRemoteDownloads.FirstOrDefault(item => item.Id == taskId);
                if (task == null || task.Status != DownloadStatus.Paused)
                {
                    return;
                }

                task.Status = DownloadStatus.Downloading;
            }

            NotifyChanged();
        }

        private async Task ExecuteRemoteDownloadAsync(DownloadTaskInfo taskInfo)
        {
            var cancellationToken = GetTaskCancellationToken(taskInfo.Id);
            await WaitUntilCanStartAsync(taskInfo, cancellationToken);
            if (taskInfo.Status == DownloadStatus.Cancelled)
            {
                return;
            }

            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }

            NotifyChanged();

            string? targetPath = null;
            try
            {
                var progress = DownloadTransferControl.CreateProgressReporter(
                    taskInfo,
                    _syncRoot,
                    cancellationToken,
                    AppGlobal.Transfers.ProgressNotifyThreshold,
                    NotifyChanged);

                var localFilePath = await _aListUploadService.DownloadFileAsync(taskInfo.SourcePath!, progress, cancellationToken);
                targetPath = localFilePath;

                await _localMusicService.SaveCompletedDownloadRecordAsync(
                    $"{AppGlobal.AList.RemoteTrackIdPrefix}{taskInfo.SourcePath}",
                    taskInfo.Title,
                    localFilePath,
                    isVideo: false,
                    remoteSourcePath: taskInfo.SourcePath);

                lock (_syncRoot)
                {
                    taskInfo.DestinationPath = localFilePath;
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
            }
            finally
            {
                DisposeTaskCancellation(taskInfo.Id);
                lock (_syncRoot)
                {
                    TrimHistory_NoLock();
                }

                NotifyChanged();
            }
        }

        private async Task ExecuteRemoteDirectoryDownloadAsync(AListDirectoryItem directoryItem, string title, DownloadTaskInfo taskInfo)
        {
            var cancellationToken = GetTaskCancellationToken(taskInfo.Id);
            await WaitUntilCanStartAsync(taskInfo, cancellationToken);
            if (taskInfo.Status == DownloadStatus.Cancelled)
            {
                return;
            }

            lock (_syncRoot)
            {
                taskInfo.Status = DownloadStatus.Downloading;
                taskInfo.ErrorMessage = null;
            }

            NotifyChanged();

            string? targetPath = null;
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

                var localDirectory = _downloadMusicDirectoryProvider.ResolveLocalDownloadDirectory(metadata?.FavoriteFolderNames);
                FileHelp.EnsureDirectoryExists(localDirectory + Path.DirectorySeparatorChar);

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

                targetPath = localMediaPath;
                var mediaProgress = DownloadTransferControl.CreateProgressReporter(
                    taskInfo,
                    _syncRoot,
                    cancellationToken,
                    AppGlobal.Transfers.ProgressNotifyThreshold,
                    NotifyChanged);
                await _aListUploadService.DownloadFileToPathAsync(mediaItem.Path, localMediaPath, mediaProgress, cancellationToken);

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
                        VideoId = $"{AppGlobal.AList.RemoteTrackIdPrefix}{mediaItem.Path}",
                        Title = resolvedTitle,
                        Author = AppGlobal.AList.RemoteAuthor,
                        LocalFilePath = localMediaPath,
                        DownloadedDate = DateTime.UtcNow,
                        RemoteSourcePath = mediaItem.Path
                    };
                }

                if (MediaFileExtensions.IsVideoFile(mediaItem.Name))
                {
                    downloadedTrack.IsVideo = true;
                }

                await _localMusicService.AddDownloadedTrackAsync(downloadedTrack);
                await _favoriteService.RestoreFavoriteFoldersForTrackAsync(
                    downloadedTrack.VideoId,
                    downloadedTrack.Title,
                    downloadedTrack.Author,
                    downloadedTrack.ThumbnailUrl,
                    downloadedTrack.LocalFilePath,
                    metadata?.FavoriteFolderNames);

                lock (_syncRoot)
                {
                    taskInfo.Title = resolvedTitle;
                    taskInfo.DestinationPath = localMediaPath;
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
            }
            finally
            {
                DisposeTaskCancellation(taskInfo.Id);
                lock (_syncRoot)
                {
                    TrimHistory_NoLock();
                }

                NotifyChanged();
            }
        }

        private CancellationToken GetTaskCancellationToken(string taskId)
        {
            lock (_syncRoot)
            {
                if (!_taskCancellation.TryGetValue(taskId, out var cts))
                {
                    cts = new CancellationTokenSource();
                    _taskCancellation[taskId] = cts;
                }

                return cts.Token;
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

        private async Task<string> ResolveDirectoryTitleAsync(string directoryPath, string currentTitle)
        {
            if (!IsLikelyMd5(currentTitle))
            {
                return currentTitle;
            }

            var normalizedDirectory = AListPathHelper.NormalizeDirectory(directoryPath);
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

        private async Task<RemoteTrackMetadata?> TryLoadRemoteMetadataAsync(IReadOnlyList<AListDirectoryItem> items, string directoryPath)
        {
            var metadataItem = items.FirstOrDefault(item =>
                !item.IsDir &&
                item.Name.Equals(RemoteTrackMetadata.FileName, StringComparison.OrdinalIgnoreCase));

            if (metadataItem == null)
            {
                var metadataPath = AListPathHelper.BuildRemotePath(directoryPath, RemoteTrackMetadata.FileName);
                return await _aListUploadService.TryDownloadJsonAsync<RemoteTrackMetadata>(metadataPath);
            }

            return await _aListUploadService.TryDownloadJsonAsync<RemoteTrackMetadata>(metadataItem.Path);
        }

        private static AListDirectoryItem? SelectMediaItem(IReadOnlyList<AListDirectoryItem> items, string? directoryKey = null)
        {
            var mediaItems = items
                .Where(item => !item.IsDir && MediaFileExtensions.IsMediaFile(item.Name))
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
                .Where(task => task.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
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
