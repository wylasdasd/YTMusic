using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using MudBlazor;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
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
        public List<AListDirectoryItem> RemoteFiles { get; private set; } = new();

        public IReadOnlyList<DownloadTaskInfo> Uploads => _uploadManager.ActiveUploads;
        public IReadOnlyList<DownloadTaskInfo> RemoteDownloads => _remoteDownloadManager.ActiveRemoteDownloads;
        public IReadOnlyCollection<string> SelectedFilePaths => _selectedFilePaths;
        public bool IsConfigured => _settingsService.IsConfigured;
        public string NormalizedRemoteDirectory => AListUploadSettingsService.NormalizeDirectory(RemoteDirectory);
        public bool HasSelection => _selectedFilePaths.Count > 0;

        public async Task LoadAsync()
        {
            BaseUrl = _settingsService.BaseUrl;
            Token = _settingsService.Token;
            RemoteDirectory = _settingsService.RemoteDirectory;
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
            _snackbar.Add("AList upload settings saved.", Severity.Success);
            await RefreshRemoteFilesAsync();
            StateHasChanged?.Invoke();
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
                StateHasChanged?.Invoke();
                return;
            }

            IsRemoteLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                RemoteFiles = (await _aListUploadService.ListDirectoryFilesAsync())
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
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
                _uploadManager.StartUpload(file.LocalFilePath, System.IO.Path.GetFileName(file.LocalFilePath));
            }

            _snackbar.Add($"Added {selectedFiles.Count} downloaded file(s) to upload queue.", Severity.Info);
        }

        public bool IsRemoteDownloaded(string remotePath)
        {
            return _remoteDownloadedPaths.Contains(remotePath);
        }

        public void DownloadRemoteFile(AListDirectoryItem item)
        {
            _remoteDownloadManager.StartRemoteDownload(item);
            _snackbar.Add($"Added '{item.Name}' to transfers.", Severity.Info);
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

        public void Dispose()
        {
            _uploadManager.OnUploadsChanged -= HandleStateChanged;
            _remoteDownloadManager.OnRemoteDownloadsChanged -= HandleStateChanged;
            _settingsService.OnChange -= HandleStateChanged;
        }
    }
}
