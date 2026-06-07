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
        public bool IsSettingsExpanded { get; set; }
        public int SelectedTabIndex { get; set; }
        public List<DownloadedTrack> DownloadedFiles { get; private set; } = new();
        public List<AListBrowserItem> RemoteFiles { get; private set; } = new();
        public string CurrentRemoteBrowsePath { get; private set; } = "/";
        public bool CanBrowseUp => !string.Equals(CurrentRemoteBrowsePath, NormalizedRemoteDirectory, StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<DownloadTaskInfo> Uploads => _uploadManager.ActiveUploads;
        public IReadOnlyList<DownloadTaskInfo> RemoteDownloads => _remoteDownloadManager.ActiveRemoteDownloads;
        public IReadOnlyCollection<string> SelectedFilePaths => _selectedFilePaths;
        public bool IsConfigured => _settingsService.IsConfigured;
        public string NormalizedRemoteDirectory => AListUploadSettingsService.NormalizeDirectory(RemoteDirectory);
        public bool HasSelection => _selectedFilePaths.Count > 0;

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

        public async Task LoadAsync()
        {
            BaseUrl = _settingsService.BaseUrl;
            Token = _settingsService.Token;
            RemoteDirectory = _settingsService.RemoteDirectory;
            CurrentRemoteBrowsePath = _settingsService.RemoteDirectory;
            IsSettingsExpanded = false;
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
            CurrentRemoteBrowsePath = _settingsService.RemoteDirectory;
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

        public async Task RefreshRemoteFilesAsync()
        {
            if (!_settingsService.IsConfigured)
            {
                RemoteFiles.Clear();
                CurrentRemoteBrowsePath = "/";
                StateHasChanged?.Invoke();
                return;
            }

            IsRemoteLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                CurrentRemoteBrowsePath = NormalizeBrowsePath(CurrentRemoteBrowsePath);

                var remoteItems = (await _aListUploadService.ListDirectoryItemsAsync(CurrentRemoteBrowsePath))
                    .OrderByDescending(item => item.IsDir)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var browserItems = new List<AListBrowserItem>(remoteItems.Count);
                foreach (var item in remoteItems)
                {
                    browserItems.Add(await ToBrowserItemAsync(item));
                }

                RemoteFiles = browserItems;
            }
            catch (Exception ex)
            {
                RemoteFiles.Clear();
                _snackbar.Add($"Failed to load AList directory: {ex.Message}", Severity.Error);
            }
            finally
            {
                IsRemoteLoading = false;
                StateHasChanged?.Invoke();
            }
        }

        public async Task OpenRemoteDirectoryAsync(AListDirectoryItem item)
        {
            if (!item.IsDir)
            {
                return;
            }

            CurrentRemoteBrowsePath = item.Path;
            await RefreshRemoteFilesAsync();
        }

        public async Task BrowseUpAsync()
        {
            if (!CanBrowseUp)
            {
                return;
            }

            var current = NormalizeBrowsePath(CurrentRemoteBrowsePath);
            var parent = GetParentPath(current);
            var root = NormalizeBrowsePath(NormalizedRemoteDirectory);
            if (parent.Length < root.Length)
            {
                parent = root;
            }

            CurrentRemoteBrowsePath = parent;
            await RefreshRemoteFilesAsync();
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
            if (item.IsDir)
            {
                var mediaTitle = await TryResolveDirectoryMediaTitleAsync(item.Path);
                if (!string.IsNullOrWhiteSpace(mediaTitle))
                {
                    return CreateBrowserItem(item, mediaTitle, item.Name);
                }
            }

            return CreateBrowserItem(item, item.Name, null);
        }

        private async Task<string?> TryResolveDirectoryMediaTitleAsync(string directoryPath)
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
                    if (!string.IsNullOrWhiteSpace(metadata?.Title))
                    {
                        return metadata.Title;
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
                    ? null
                    : Path.GetFileNameWithoutExtension(mediaItem.Name);
            }
            catch
            {
                return null;
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

        private static AListBrowserItem CreateBrowserItem(AListDirectoryItem item, string displayName, string? subtitle)
        {
            return new AListBrowserItem
            {
                Item = item,
                DisplayName = displayName,
                Subtitle = subtitle
            };
        }

        private string NormalizeBrowsePath(string? path)
        {
            return AListUploadSettingsService.NormalizeDirectory(string.IsNullOrWhiteSpace(path) ? NormalizedRemoteDirectory : path);
        }

        private static string GetParentPath(string path)
        {
            var normalized = AListUploadSettingsService.NormalizeDirectory(path);
            if (normalized == "/")
            {
                return "/";
            }

            var lastSlash = normalized.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return "/";
            }

            return normalized[..lastSlash];
        }

        public void Dispose()
        {
            _uploadManager.OnUploadsChanged -= HandleStateChanged;
            _remoteDownloadManager.OnRemoteDownloadsChanged -= HandleStateChanged;
            _settingsService.OnChange -= HandleStateChanged;
        }
    }
}
