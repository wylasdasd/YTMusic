using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommonTool.FileHelps;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Infrastructure.AList;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services
{
    public class AListUploadService : IAListUploadService
    {
        private const long MaxInMemoryUploadBytes = AppGlobal.AList.MaxInMemoryUploadBytes;

        private readonly IAListUploadSettingsService _settingsService;
        private readonly IDownloadMusicDirectoryProvider _downloadMusicDirectoryProvider;
        private readonly AListFsApiClient _fsApiClient;

        public AListUploadService(
            IAListUploadSettingsService settingsService,
            IDownloadMusicDirectoryProvider downloadMusicDirectoryProvider,
            AListFsApiClient fsApiClient)
        {
            _settingsService = settingsService;
            _downloadMusicDirectoryProvider = downloadMusicDirectoryProvider;
            _fsApiClient = fsApiClient;
        }

        public async Task<string> UploadFileAsync(string localFilePath, string? displayName, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
            }

            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                throw new FileNotFoundException("The selected file no longer exists.", localFilePath);
            }

            var fileName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(localFilePath) : displayName.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Unable to determine file name.");
            }

            var remoteFilePath = AListPathHelper.BuildRemotePath(_settingsService.RemoteDirectory, fileName);
            await UploadFileToPathAsync(localFilePath, remoteFilePath, progress, cancellationToken);
            return remoteFilePath;
        }

        public async Task<string> UploadFileToPathAsync(string localFilePath, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                throw new FileNotFoundException("The selected file no longer exists.", localFilePath);
            }

            var expectedSize = new FileInfo(localFilePath).Length;
            if (expectedSize <= 0)
            {
                throw new InvalidOperationException("The selected file is empty.");
            }

            byte[]? payload = null;
            if (expectedSize <= MaxInMemoryUploadBytes)
            {
                payload = await File.ReadAllBytesAsync(localFilePath, cancellationToken);
                if (payload.LongLength != expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Local file read incomplete. Expected {expectedSize} bytes, read {payload.LongLength} bytes.");
                }
            }

            const int maxAttempts = AppGlobal.AList.UploadMaxAttempts;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (payload != null)
                    {
                        await using var memoryStream = new MemoryStream(payload, writable: false);
                        await _fsApiClient.UploadStreamToPathAsync(memoryStream, remoteFilePath, progress, cancellationToken);
                    }
                    else
                    {
                        await _fsApiClient.UploadLargeFileToPathAsync(localFilePath, remoteFilePath, cancellationToken);
                    }

                    // 轮询校验远端大小，避免元数据未刷新误报，同时能发现上传不完整。
                    await VerifyRemoteFileSizeWithRetryAsync(remoteFilePath, expectedSize, cancellationToken);
                    progress?.Report(1.0);

                    return remoteFilePath;
                }
                catch (Exception ex) when (attempt < maxAttempts && AListApiHelpers.IsRetryableUploadException(ex))
                {
                    lastError = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
                }
            }

            throw lastError ?? new InvalidOperationException("Upload failed.");
        }

        public async Task CreateDirectoryAsync(string remoteDirectoryPath, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server and token settings first.");
            }

            if (string.IsNullOrWhiteSpace(remoteDirectoryPath))
            {
                throw new InvalidOperationException("Remote directory path is required.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BaseUrl}/api/fs/mkdir");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Content = JsonContent.Create(new
            {
                path = remoteDirectoryPath
            });

            using var response = await AListHttpClients.Default.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(body, $"Create directory failed with HTTP {(int)response.StatusCode}."));
            }

            var apiMessage = AListApiHelpers.GetApiMessage(body);
            if (!string.IsNullOrWhiteSpace(apiMessage) &&
                !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Contains("exist", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(apiMessage);
            }
        }

        public async Task<string> UploadCoverFromUrlAsync(string coverUrl, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                throw new InvalidOperationException("Cover URL is required.");
            }

            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, coverUrl);
            downloadRequest.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            using var downloadResponse = await AListHttpClients.Default.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                var body = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(body, $"Failed to fetch cover with HTTP {(int)downloadResponse.StatusCode}."));
            }

            var extension = ".jpg";
            try
            {
                var uri = new Uri(coverUrl, UriKind.Absolute);
                var candidate = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 5)
                {
                    extension = candidate;
                }
            }
            catch
            {
            }

            await using var coverStream = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
            var tempPath = await AListFileHelpers.WriteTempFileAsync(coverStream, extension, cancellationToken);
            try
            {
                return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
            }
            finally
            {
                AListFileHelpers.TryDeleteFile(tempPath);
            }
        }

        public async Task<string> UploadCoverAsync(string coverSource, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(coverSource))
            {
                throw new InvalidOperationException("Cover source is required.");
            }

            if (AListFileHelpers.TryGetLocalPath(coverSource, out var localPath))
            {
                return await UploadFileToPathAsync(localPath, remoteFilePath, progress, cancellationToken);
            }

            if (coverSource.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                await using var dataStream = AListFileHelpers.CreateDataUriStream(coverSource);
                var tempPath = await AListFileHelpers.WriteTempFileAsync(dataStream, ".jpg", cancellationToken);
                try
                {
                    return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
                }
                finally
                {
                    AListFileHelpers.TryDeleteFile(tempPath);
                }
            }

            return await UploadCoverFromUrlAsync(coverSource, remoteFilePath, progress, cancellationToken);
        }

        public async Task<string> UploadJsonToPathAsync<T>(T payload, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(payload, AListHttpClients.JsonOptions);
            var tempPath = Path.Combine(Path.GetTempPath(), $"ytmusic-meta-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);
            try
            {
                return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
            }
            finally
            {
                AListFileHelpers.TryDeleteFile(tempPath);
            }
        }

        public async Task<T?> TryDownloadJsonAsync<T>(string remotePath, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured || string.IsNullOrWhiteSpace(remotePath))
            {
                return default;
            }

            try
            {
                using var metadataRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BaseUrl}/api/fs/get");
                metadataRequest.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
                metadataRequest.Content = JsonContent.Create(new { path = remotePath });

                using var metadataResponse = await AListHttpClients.Default.SendAsync(metadataRequest, cancellationToken);
                var metadataBody = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    return default;
                }

                using var metadataDocument = JsonDocument.Parse(metadataBody);
                var downloadUrl = AListApiHelpers.GetDownloadUrl(metadataDocument);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return default;
                }

                using var response = await _fsApiClient.SendDownloadRequestAsync(downloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return default;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<T>(responseStream, AListHttpClients.JsonOptions, cancellationToken);
            }
            catch
            {
                return default;
            }
        }

        public async Task<IReadOnlyList<AListDirectoryItem>> ListDirectoryItemsAsync(string? remotePath = null, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
            }

            var targetPath = string.IsNullOrWhiteSpace(remotePath)
                ? _settingsService.RemoteDirectory
                : AListPathHelper.NormalizeDirectory(remotePath);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BaseUrl}/api/fs/list");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Content = JsonContent.Create(new
            {
                path = targetPath,
                password = string.Empty,
                page = 1,
                per_page = 0,
                refresh = false
            });

            using var response = await AListHttpClients.Default.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(body, $"List failed with HTTP {(int)response.StatusCode}."));
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                var apiMessage = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(apiMessage) &&
                    !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                    !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(apiMessage);
                }
            }

            var result = new List<AListDirectoryItem>();
            if (!document.RootElement.TryGetProperty("data", out var dataElement))
            {
                return result;
            }

            if (!dataElement.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in contentElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var isDir = false;
                if (item.TryGetProperty("is_dir", out var isDirElement) && isDirElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    isDir = isDirElement.GetBoolean();
                }

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                long size = 0;
                if (item.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var sizeValue))
                {
                    size = sizeValue;
                }

                DateTime? modifiedAt = null;
                if (item.TryGetProperty("modified", out var modifiedElement))
                {
                    var modifiedText = modifiedElement.GetString();
                    if (DateTime.TryParse(modifiedText, out var modified))
                    {
                        modifiedAt = modified;
                    }
                }

                result.Add(new AListDirectoryItem
                {
                    Name = name,
                    Path = AListPathHelper.BuildRemotePath(targetPath, name),
                    Size = size,
                    IsDir = isDir,
                    ModifiedAt = modifiedAt
                });
            }

            return result;
        }

        public async Task<string> DownloadFileAsync(string remotePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(remotePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Unable to determine remote file name.");
            }

            var localDirectory = _downloadMusicDirectoryProvider.GetDownloadedMusicDirectory();
            FileHelp.EnsureDirectoryExists(localDirectory);
            var localFilePath = Path.Combine(localDirectory, FileHelp.SafeFileName(fileName));
            return await DownloadFileToPathAsync(remotePath, localFilePath, progress, cancellationToken);
        }

        public async Task<string> DownloadFileToPathAsync(string remotePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                throw new InvalidOperationException("Remote file path is required.");
            }

            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                throw new InvalidOperationException("Local file path is required.");
            }

            FileHelp.EnsureDirectoryExists(localFilePath);

            using var metadataRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BaseUrl}/api/fs/get");
            metadataRequest.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            metadataRequest.Content = JsonContent.Create(new
            {
                path = remotePath
            });

            using var metadataResponse = await AListHttpClients.Default.SendAsync(metadataRequest, cancellationToken);
            var metadataBody = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!metadataResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(metadataBody, $"Get file metadata failed with HTTP {(int)metadataResponse.StatusCode}."));
            }

            using var metadataDocument = JsonDocument.Parse(metadataBody);
            var downloadUrl = AListApiHelpers.GetDownloadUrl(metadataDocument);
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("AList did not return a downloadable URL for this file.");
            }

            using var response = await _fsApiClient.SendDownloadRequestAsync(downloadUrl, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(localFilePath);

            var totalLength = response.Content.Headers.ContentLength ?? -1;
            var buffer = new byte[81_920];
            long totalRead = 0;
            int read;

            while ((read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;

                if (totalLength > 0)
                {
                    progress?.Report((double)totalRead / totalLength);
                }
            }

            progress?.Report(1.0);
            return localFilePath;
        }

        private async Task VerifyRemoteFileSizeWithRetryAsync(string remoteFilePath, long expectedSize, CancellationToken cancellationToken)
        {
            const int maxAttempts = AppGlobal.AList.UploadVerifyMaxAttempts;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await VerifyRemoteFileSizeAsync(remoteFilePath, expectedSize, cancellationToken);
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    lastError = ex;
                    if (attempt >= maxAttempts)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }

            throw lastError ?? new InvalidOperationException("Upload verification failed.");
        }

        private async Task VerifyRemoteFileSizeAsync(string remoteFilePath, long expectedSize, CancellationToken cancellationToken)
        {
            var parentPath = AListApiHelpers.GetRemoteParentPath(remoteFilePath);
            var fileName = AListApiHelpers.GetRemoteFileName(remoteFilePath);
            var items = await ListDirectoryItemsAsync(parentPath, cancellationToken);
            var remoteItem = items.FirstOrDefault(item =>
                !item.IsDir && item.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (remoteItem == null)
            {
                throw new InvalidOperationException("Upload finished but the remote file was not found.");
            }

            if (remoteItem.Size != expectedSize)
            {
                throw new InvalidOperationException(
                    $"Upload size mismatch. Local: {expectedSize} bytes, remote: {remoteItem.Size} bytes.");
            }
        }
    }
}
