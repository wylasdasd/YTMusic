using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using MudBlazor;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
    public class AListBrowserItem
    {
        public required AListDirectoryItem Item { get; init; }
        public required string DisplayName { get; init; }
        public string? Subtitle { get; init; }
        public string? ThumbnailUrl { get; init; }
    }

    public class UploadVM : IDisposable
    {
        private readonly AListUploadSettingsService _settingsService;
        private readonly AListUploadService _aListUploadService;
        private readonly IUploadManagerService _uploadManager;
        private readonly IAListRemoteDownloadManagerService _remoteDownloadManager;
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;
        private readonly HashSet<string> _selectedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedRemotePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _remoteDownloadedPaths = new(StringComparer.OrdinalIgnoreCase);

        public UploadVM(
            AListUploadSettingsService settingsService,
            AListUploadService aListUploadService,
            IUploadManagerService uploadManager,
            IAListRemoteDownloadManagerService remoteDownloadManager,
            ILocalMusicService localMusicService,
            ISnackbar snackbar)
        {
            _settingsService = settingsService;
            _aListUploadService = aListUploadService;
            _uploadManager = uploadManager;
            _remoteDownloadManager = remoteDownloadManager;
            _localMusicService = localMusicService;
            _snackbar = snackbar;

            _uploadManager.OnUploadsChanged += HandleStateChanged;
            _remoteDownloadManager.OnRemoteDownloadsChanged += HandleStateChanged;
            _settingsService.OnChange += HandleStateChanged;
        }

        public Action? StateHasChanged { get; set; }

        public string BaseUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string RemoteDirectory { get; set; } = "/";
        public bool IsLoading { get; private set; }
        public bool IsRemoteLoading { get; private set; }
        public const int LocalTabIndex = 0;
        public const int RemoteTabIndex = 1;
        public const int SettingsTabIndex = 2;

        public int SelectedTabIndex { get; set; } = RemoteTabIndex;
        public List<DownloadedTrack> DownloadedFiles { get; private set; } = new();
        public List<AListBrowserItem> RemoteFiles { get; private set; } = new();

        public IReadOnlyList<DownloadTaskInfo> Uploads => _uploadManager.ActiveUploads;
        public IReadOnlyList<DownloadTaskInfo> RemoteDownloads => _remoteDownloadManager.ActiveRemoteDownloads;
        public IReadOnlyCollection<string> SelectedFilePaths => _selectedFilePaths;
        public IReadOnlyCollection<string> SelectedRemotePaths => _selectedRemotePaths;
        public bool IsConfigured => _settingsService.IsConfigured;
        public string NormalizedRemoteDirectory => AListUploadSettingsService.NormalizeDirectory(RemoteDirectory);
        public bool HasSelection => _selectedFilePaths.Count > 0;
        public bool HasRemoteSelection => _selectedRemotePaths.Count > 0;

        public bool IsAllSelected =>
            DownloadedFiles.Count > 0 &&
            DownloadedFiles.All(file => _selectedFilePaths.Contains(file.LocalFilePath));

        public bool IsAllRemoteSelected =>
            RemoteFiles.Count > 0 &&
            RemoteFiles.All(file => _selectedRemotePaths.Contains(file.Item.Path));

        public DownloadTaskInfo? GetLatestUploadForFile(string localFilePath)
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                return null;
            }

            return Uploads
                .Where(task => !string.IsNullOrWhiteSpace(task.SourcePath) &&
                               string.Equals(task.SourcePath, localFilePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(task => task.CreatedAtUtc)
                .FirstOrDefault();
        }

        public bool IsUploadInProgress(string localFilePath)
        {
            var task = GetLatestUploadForFile(localFilePath);
            return task != null &&
                   task.Status is DownloadStatus.Pending or DownloadStatus.Downloading;
        }

        public bool ShouldShowUploadedTag(DownloadedTrack file)
        {
            return file.HasUploaded && !IsUploadInProgress(file.LocalFilePath);
        }

        public DownloadTaskInfo? GetLatestRemoteDownloadForPath(string remotePath)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return null;
            }

            return RemoteDownloads
                .Where(task => !string.IsNullOrWhiteSpace(task.SourcePath) &&
                               string.Equals(task.SourcePath, remotePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(task => task.CreatedAtUtc)
                .FirstOrDefault();
        }

        public bool IsRemoteDownloadInProgress(string remotePath)
        {
            var task = GetLatestRemoteDownloadForPath(remotePath);
            return task != null &&
                   task.Status is DownloadStatus.Pending or DownloadStatus.Downloading;
        }

        public bool ShouldShowDownloadedTag(string remoteDirectoryPath)
        {
            return IsRemoteDirectoryDownloaded(remoteDirectoryPath) &&
                   !IsRemoteDownloadInProgress(remoteDirectoryPath);
        }

        public async Task LoadAsync()
        {
            BaseUrl = _settingsService.BaseUrl;
            Token = _settingsService.Token;
            RemoteDirectory = _settingsService.RemoteDirectory;
            await RefreshDownloadedFilesAsync();
            await RefreshRemoteFilesAsync();
            StateHasChanged?.Invoke();
        }

        public async Task SaveSettingsAsync()
        {
            _settingsService.Save(BaseUrl, Token, RemoteDirectory);
            BaseUrl = _settingsService.BaseUrl;
            Token = _settingsService.Token;
            RemoteDirectory = _settingsService.RemoteDirectory;
            _snackbar.Add("AList upload settings saved.", Severity.Success);
            await RefreshRemoteFilesAsync();
            StateHasChanged?.Invoke();
        }

        public async Task CreateRemoteDirectoryAsync()
        {
            if (!_settingsService.IsConfigured ||
                !string.Equals(BaseUrl, _settingsService.BaseUrl, StringComparison.Ordinal) ||
                !string.Equals(Token, _settingsService.Token, StringComparison.Ordinal) ||
                !string.Equals(AListUploadSettingsService.NormalizeDirectory(RemoteDirectory), _settingsService.RemoteDirectory, StringComparison.Ordinal))
            {
                await SaveSettingsAsync();
            }

            if (!_settingsService.IsConfigured)
            {
                _snackbar.Add("Please complete AList server, token, and remote directory settings first.", Severity.Warning);
                return;
            }

            try
            {
                await _aListUploadService.CreateDirectoryAsync(_settingsService.RemoteDirectory);
                _snackbar.Add($"Directory ready: {_settingsService.RemoteDirectory}", Severity.Success);
                await RefreshRemoteFilesAsync();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to create directory: {ex.Message}", Severity.Error);
            }
        }

        public async Task RefreshDownloadedFilesAsync()
        {
            IsLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                DownloadedFiles = (await _localMusicService.GetDownloadedTracksAsync())
                    .OrderByDescending(file => file.DownloadedDate)
                    .ToList();

                _remoteDownloadedPaths.Clear();
                foreach (var path in DownloadedFiles
                    .Where(file => !string.IsNullOrWhiteSpace(file.RemoteSourcePath))
                    .Select(file => file.RemoteSourcePath!))
                {
                    _remoteDownloadedPaths.Add(path);
                }

                _selectedFilePaths.RemoveWhere(path => DownloadedFiles.All(file => !string.Equals(file.LocalFilePath, path, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                IsLoading = false;
                StateHasChanged?.Invoke();
            }
        }

        public void ToggleSelection(string localFilePath, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                return;
            }

            if (isSelected)
            {
                _selectedFilePaths.Add(localFilePath);
            }
            else
            {
                _selectedFilePaths.Remove(localFilePath);
            }

            StateHasChanged?.Invoke();
        }

        public async Task OnRemoteTabActivatedAsync()
        {
            await SyncDownloadedTracksForRemoteBadgesAsync();
            await RefreshRemoteFilesAsync();
        }

        private async Task SyncDownloadedTracksForRemoteBadgesAsync()
        {
            try
            {
                DownloadedFiles = (await _localMusicService.GetDownloadedTracksAsync())
                    .OrderByDescending(file => file.DownloadedDate)
                    .ToList();

                _remoteDownloadedPaths.Clear();
                foreach (var path in DownloadedFiles
                    .Where(file => !string.IsNullOrWhiteSpace(file.RemoteSourcePath))
                    .Select(file => file.RemoteSourcePath!))
                {
                    _remoteDownloadedPaths.Add(path);
                }
            }
            catch
            {
                // 静默同步，避免切 tab 时打断列表刷新。
            }
        }

        public async Task RefreshRemoteFilesAsync()
        {
            if (!_settingsService.IsConfigured)
            {
                RemoteFiles.Clear();
                StateHasChanged?.Invoke();
                return;
            }

            IsRemoteLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                var remoteItems = (await _aListUploadService.ListDirectoryItemsAsync(NormalizedRemoteDirectory))
                    .Where(item => item.IsDir)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var browserItems = new List<AListBrowserItem>(remoteItems.Count);
                foreach (var item in remoteItems)
                {
                    browserItems.Add(await ToBrowserItemAsync(item));
                }

                RemoteFiles = browserItems;
                _selectedRemotePaths.RemoveWhere(path =>
                    RemoteFiles.All(file => !string.Equals(file.Item.Path, path, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                RemoteFiles.Clear();
                _selectedRemotePaths.Clear();
                _snackbar.Add($"Failed to load AList directory: {ex.Message}", Severity.Error);
            }
            finally
            {
                IsRemoteLoading = false;
                StateHasChanged?.Invoke();
            }
        }

        public void ToggleSelectAll(bool isSelected)
        {
            _selectedFilePaths.Clear();

            if (isSelected)
            {
                foreach (var file in DownloadedFiles)
                {
                    _selectedFilePaths.Add(file.LocalFilePath);
                }
            }

            StateHasChanged?.Invoke();
        }

        public bool IsSelected(string localFilePath)
        {
            return _selectedFilePaths.Contains(localFilePath);
        }

        public bool IsRemoteSelected(string remotePath)
        {
            return _selectedRemotePaths.Contains(remotePath);
        }

        public void ToggleRemoteSelection(string remotePath, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return;
            }

            if (isSelected)
            {
                _selectedRemotePaths.Add(remotePath);
            }
            else
            {
                _selectedRemotePaths.Remove(remotePath);
            }

            StateHasChanged?.Invoke();
        }

        public void ToggleRemoteSelectAll(bool isSelected)
        {
            _selectedRemotePaths.Clear();

            if (isSelected)
            {
                foreach (var file in RemoteFiles)
                {
                    _selectedRemotePaths.Add(file.Item.Path);
                }
            }

            StateHasChanged?.Invoke();
        }

        public async Task UploadSelectedAsync()
        {
            if (!_settingsService.IsConfigured)
            {
                await SaveSettingsAsync();
            }

            if (!_settingsService.IsConfigured)
            {
                _snackbar.Add("Please complete AList server, token, and remote directory settings first.", Severity.Warning);
                return;
            }

            var selectedFiles = DownloadedFiles
                .Where(file => _selectedFilePaths.Contains(file.LocalFilePath))
                .ToList();

            if (selectedFiles.Count == 0)
            {
                _snackbar.Add("Please select downloaded files first.", Severity.Info);
                return;
            }

            foreach (var file in selectedFiles)
            {
                _uploadManager.StartUpload(file);
            }

            _snackbar.Add($"Added {selectedFiles.Count} downloaded file(s) to upload queue.", Severity.Info);
        }

        public bool IsRemoteDownloaded(string remotePath)
        {
            return _remoteDownloadedPaths.Contains(remotePath);
        }

        public bool IsRemoteDirectoryDownloaded(string remoteDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(remoteDirectoryPath))
            {
                return false;
            }

            var normalized = AListUploadSettingsService.NormalizeDirectory(remoteDirectoryPath);
            return DownloadedFiles.Any(file =>
                !string.IsNullOrWhiteSpace(file.RemoteSourcePath) &&
                file.RemoteSourcePath!.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));
        }

        public void DownloadRemoteFile(AListDirectoryItem item)
        {
            _remoteDownloadManager.StartRemoteDownload(item);
            _snackbar.Add($"Added '{item.Name}' to transfers.", Severity.Info);
        }

        public void DownloadRemoteDirectory(AListBrowserItem item)
        {
            _remoteDownloadManager.StartRemoteDirectoryDownload(item.Item, item.DisplayName);
            _snackbar.Add($"Added '{item.DisplayName}' to transfers.", Severity.Info);
        }

        public void DownloadSelectedRemoteAsync()
        {
            if (!_settingsService.IsConfigured)
            {
                _snackbar.Add("Please complete AList server, token, and remote directory settings first.", Severity.Warning);
                return;
            }

            var selectedFiles = RemoteFiles
                .Where(file => _selectedRemotePaths.Contains(file.Item.Path))
                .ToList();

            if (selectedFiles.Count == 0)
            {
                _snackbar.Add("Please select remote items first.", Severity.Info);
                return;
            }

            var addedCount = 0;
            foreach (var file in selectedFiles)
            {
                if (IsRemoteDirectoryDownloaded(file.Item.Path) || IsRemoteDownloadInProgress(file.Item.Path))
                {
                    continue;
                }

                _remoteDownloadManager.StartRemoteDirectoryDownload(file.Item, file.DisplayName);
                addedCount++;
            }

            if (addedCount > 0)
            {
                _snackbar.Add($"Added {addedCount} remote item(s) to download queue.", Severity.Info);
            }
            else
            {
                _snackbar.Add("Selected items are already downloaded or in progress.", Severity.Info);
            }
        }

        public async Task PickAndUploadFilesAsync()
        {
            if (!_settingsService.IsConfigured)
            {
                await SaveSettingsAsync();
            }

            if (!_settingsService.IsConfigured)
            {
                _snackbar.Add("Please complete AList server, token, and remote directory settings first.", Severity.Warning);
                return;
            }

            try
            {
                var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select files to upload to AList"
                });

                if (files == null)
                {
                    return;
                }

                var addedCount = 0;
                foreach (var file in files)
                {
                    if (file == null)
                    {
                        continue;
                    }

                    var fullPath = file.FullPath;
                    if (string.IsNullOrWhiteSpace(fullPath))
                    {
                        _snackbar.Add($"'{file.FileName}' cannot be uploaded on this platform because full file path is unavailable.", Severity.Warning);
                        continue;
                    }

                    _uploadManager.StartUpload(fullPath, file.FileName);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    _snackbar.Add($"Added {addedCount} file(s) to upload queue.", Severity.Info);
                }
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to pick files: {ex.Message}", Severity.Error);
            }
        }

        private void HandleStateChanged()
        {
            var needsRefresh = Uploads.Any(upload =>
                upload.Status == DownloadStatus.Completed &&
                !string.IsNullOrWhiteSpace(upload.SourcePath) &&
                DownloadedFiles.Any(file =>
                    string.Equals(file.LocalFilePath, upload.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                    !file.HasUploaded));

            var remoteNeedsRefresh = RemoteDownloads.Any(download =>
                download.Status == DownloadStatus.Completed &&
                !string.IsNullOrWhiteSpace(download.SourcePath) &&
                !_remoteDownloadedPaths.Contains(download.SourcePath));

            if (needsRefresh || remoteNeedsRefresh)
            {
                _ = RefreshDownloadedFilesAsync();
                return;
            }

            StateHasChanged?.Invoke();
        }

        private async Task<AListBrowserItem> ToBrowserItemAsync(AListDirectoryItem item)
        {
            var (title, thumbnailUrl) = await TryResolveDirectoryInfoAsync(item.Path);
            var displayName = string.IsNullOrWhiteSpace(title) ? item.Name : title;
            return CreateBrowserItem(item, displayName, item.Name, thumbnailUrl);
        }

        private async Task<(string? Title, string? ThumbnailUrl)> TryResolveDirectoryInfoAsync(string directoryPath)
        {
            try
            {
                var children = await _aListUploadService.ListDirectoryItemsAsync(directoryPath);
                var metadataItem = children.FirstOrDefault(child =>
                    !child.IsDir &&
                    child.Name.Equals(RemoteTrackMetadata.FileName, StringComparison.OrdinalIgnoreCase));
                if (metadataItem != null)
                {
                    var metadata = await _aListUploadService.TryDownloadJsonAsync<RemoteTrackMetadata>(metadataItem.Path);
                    if (metadata != null)
                    {
                        return (metadata.Title, metadata.ThumbnailUrl);
                    }
                }

                var directoryKey = directoryPath.TrimEnd('/').Split('/').LastOrDefault();
                var mediaItem = children
                    .Where(child => !child.IsDir && IsMediaFile(child.Name))
                    .OrderByDescending(child =>
                        !string.IsNullOrWhiteSpace(directoryKey) &&
                        string.Equals(Path.GetFileNameWithoutExtension(child.Name), directoryKey, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(child => child.Size)
                    .FirstOrDefault();

                return mediaItem == null
                    ? (null, null)
                    : (Path.GetFileNameWithoutExtension(mediaItem.Name), null);
            }
            catch
            {
                return (null, null);
            }
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

        private static AListBrowserItem CreateBrowserItem(AListDirectoryItem item, string displayName, string? subtitle, string? thumbnailUrl = null)
        {
            return new AListBrowserItem
            {
                Item = item,
                DisplayName = displayName,
                Subtitle = subtitle,
                ThumbnailUrl = thumbnailUrl
            };
        }

        public void Dispose()
        {
            _uploadManager.OnUploadsChanged -= HandleStateChanged;
            _remoteDownloadManager.OnRemoteDownloadsChanged -= HandleStateChanged;
            _settingsService.OnChange -= HandleStateChanged;
        }
    }
}
