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
using YTMusic.BLL.Models;

namespace YTMusic.Services
{
    public class AListUploadService : IAListUploadService
    {
        private const long MaxInMemoryUploadBytes = 512L * 1024 * 1024;

        private static readonly HttpClient _httpClient = new();
        private static readonly HttpClient _uploadHttpClient = new()
        {
            // 默认 100s 超时在大文件/慢网上会中断 PUT，远端可能只留下半截文件。
            Timeout = TimeSpan.FromHours(6)
        };
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        private readonly AListUploadSettingsService _settingsService;

        public AListUploadService(AListUploadSettingsService settingsService)
        {
            _settingsService = settingsService;
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

            var remoteFilePath = CombineRemotePath(_settingsService.RemoteDirectory, fileName);
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

            const int maxAttempts = 3;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (payload != null)
                    {
                        await using var memoryStream = new MemoryStream(payload, writable: false);
                        await UploadStreamToPathAsync(memoryStream, remoteFilePath, progress, cancellationToken);
                    }
                    else
                    {
                        await UploadLargeFileToPathAsync(localFilePath, remoteFilePath, progress, cancellationToken);
                    }

                    // 轮询校验远端大小，避免元数据未刷新误报，同时能发现上传不完整。
                    await VerifyRemoteFileSizeWithRetryAsync(remoteFilePath, expectedSize, cancellationToken);
                    progress?.Report(1.0);

                    return remoteFilePath;
                }
                catch (Exception ex) when (attempt < maxAttempts && IsRetryableUploadException(ex))
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

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(GetErrorMessage(body, $"Create directory failed with HTTP {(int)response.StatusCode}."));
            }

            var apiMessage = GetApiMessage(body);
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
            using var downloadResponse = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                var body = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(GetErrorMessage(body, $"Failed to fetch cover with HTTP {(int)downloadResponse.StatusCode}."));
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
            var tempPath = await WriteTempFileAsync(coverStream, extension, cancellationToken);
            try
            {
                return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        public async Task<string> UploadCoverAsync(string coverSource, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(coverSource))
            {
                throw new InvalidOperationException("Cover source is required.");
            }

            if (TryGetLocalPath(coverSource, out var localPath))
            {
                return await UploadFileToPathAsync(localPath, remoteFilePath, progress, cancellationToken);
            }

            if (coverSource.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                await using var dataStream = CreateDataUriStream(coverSource);
                var tempPath = await WriteTempFileAsync(dataStream, ".jpg", cancellationToken);
                try
                {
                    return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
                }
                finally
                {
                    TryDeleteFile(tempPath);
                }
            }

            return await UploadCoverFromUrlAsync(coverSource, remoteFilePath, progress, cancellationToken);
        }

        public async Task<string> UploadJsonToPathAsync<T>(T payload, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var tempPath = Path.Combine(Path.GetTempPath(), $"ytmusic-meta-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);
            try
            {
                return await UploadFileToPathAsync(tempPath, remoteFilePath, progress, cancellationToken);
            }
            finally
            {
                TryDeleteFile(tempPath);
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

                using var metadataResponse = await _httpClient.SendAsync(metadataRequest, cancellationToken);
                var metadataBody = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    return default;
                }

                using var metadataDocument = JsonDocument.Parse(metadataBody);
                var downloadUrl = GetDownloadUrl(metadataDocument);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return default;
                }

                using var response = await SendDownloadRequestAsync(downloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return default;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<T>(responseStream, JsonOptions, cancellationToken);
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
                : AListUploadSettingsService.NormalizeDirectory(remotePath);

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

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(GetErrorMessage(body, $"List failed with HTTP {(int)response.StatusCode}."));
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
                    Path = CombineRemotePath(targetPath, name),
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

            var localDirectory = StoragePaths.GetDownloadedMusicDirectory();
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

            using var metadataResponse = await _httpClient.SendAsync(metadataRequest, cancellationToken);
            var metadataBody = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!metadataResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(GetErrorMessage(metadataBody, $"Get file metadata failed with HTTP {(int)metadataResponse.StatusCode}."));
            }

            using var metadataDocument = JsonDocument.Parse(metadataBody);
            var downloadUrl = GetDownloadUrl(metadataDocument);
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("AList did not return a downloadable URL for this file.");
            }

            using var response = await SendDownloadRequestAsync(downloadUrl, cancellationToken);

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

        private static string? GetDownloadUrl(JsonDocument document)
        {
            if (!document.RootElement.TryGetProperty("message", out var messageElement))
            {
                return null;
            }

            var apiMessage = messageElement.GetString();
            if (!string.IsNullOrWhiteSpace(apiMessage) &&
                !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(apiMessage);
            }

            if (!document.RootElement.TryGetProperty("data", out var dataElement))
            {
                return null;
            }

            if (dataElement.TryGetProperty("raw_url", out var rawUrlElement))
            {
                var rawUrl = rawUrlElement.GetString();
                if (!string.IsNullOrWhiteSpace(rawUrl))
                {
                    return rawUrl;
                }
            }

            if (dataElement.TryGetProperty("url", out var urlElement))
            {
                var url = urlElement.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }

            return null;
        }

        private static string CombineRemotePath(string directory, string fileName)
        {
            var normalizedDirectory = AListUploadSettingsService.NormalizeDirectory(directory);
            var safeFileName = fileName.Replace('\\', '/').TrimStart('/');
            return normalizedDirectory == "/"
                ? "/" + safeFileName
                : normalizedDirectory + "/" + safeFileName;
        }

        public static string BuildRemotePath(string directory, string fileName)
        {
            return CombineRemotePath(directory, fileName);
        }

        private static string EncodeRemotePath(string remotePath)
        {
            return string.Join("/", remotePath
                .Split('/', StringSplitOptions.None)
                .Select(WebUtility.UrlEncode));
        }

        private static string? GetApiMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString();
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private static string GetErrorMessage(string responseBody, string fallback)
        {
            return GetApiMessage(responseBody) ?? fallback;
        }

        private async Task<HttpResponseMessage> SendDownloadRequestAsync(string downloadUrl, CancellationToken cancellationToken)
        {
            using var firstRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            var response = await _httpClient.SendAsync(firstRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized && response.StatusCode != HttpStatusCode.Forbidden)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                throw new InvalidOperationException(GetErrorMessage(body, $"Download failed with HTTP {(int)response.StatusCode}."));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();

            using var secondRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            secondRequest.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);

            var retryResponse = await _httpClient.SendAsync(secondRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (retryResponse.IsSuccessStatusCode)
            {
                return retryResponse;
            }

            var retryBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);
            retryResponse.Dispose();
            throw new InvalidOperationException(GetErrorMessage(retryBody, GetErrorMessage(responseBody, $"Download failed with HTTP 401/403.")));
        }

        private static bool TryGetLocalPath(string source, out string localPath)
        {
            localPath = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (File.Exists(source))
            {
                localPath = source;
                return true;
            }

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                uri.IsFile &&
                File.Exists(uri.LocalPath))
            {
                localPath = uri.LocalPath;
                return true;
            }

            return false;
        }

        private static MemoryStream CreateDataUriStream(string dataUri)
        {
            var commaIndex = dataUri.IndexOf(',');
            if (commaIndex <= 0 || commaIndex == dataUri.Length - 1)
            {
                throw new InvalidOperationException("Invalid data URI cover content.");
            }

            var metadata = dataUri[..commaIndex];
            var payload = dataUri[(commaIndex + 1)..];
            if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only base64 data URI cover content is supported.");
            }

            try
            {
                return new MemoryStream(Convert.FromBase64String(payload), writable: false);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Invalid base64 cover content.", ex);
            }
        }

        private static async Task<string> WriteTempFileAsync(Stream sourceStream, string extension, CancellationToken cancellationToken)
        {
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension;
            var tempPath = Path.Combine(Path.GetTempPath(), $"ytmusic-upload-{Guid.NewGuid():N}{safeExtension}");
            await using (var fileStream = File.Create(tempPath))
            {
                await sourceStream.CopyToAsync(fileStream, cancellationToken);
            }

            return tempPath;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private async Task UploadStreamToPathAsync(Stream sourceStream, string remoteFilePath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server and token settings first.");
            }

            if (string.IsNullOrWhiteSpace(remoteFilePath))
            {
                throw new InvalidOperationException("Remote upload path is required.");
            }

            var encodedRemoteFilePath = EncodeRemotePath(remoteFilePath);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"{_settingsService.BaseUrl}/api/fs/put");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Headers.TryAddWithoutValidation("File-Path", encodedRemoteFilePath);
            request.Content = new ProgressStreamContent(sourceStream, progress, disposeSource: false);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _uploadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(GetErrorMessage(body, $"Upload failed with HTTP {(int)response.StatusCode}."));
            }

            var apiMessage = GetApiMessage(body);
            if (!string.IsNullOrWhiteSpace(apiMessage) &&
                !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(apiMessage);
            }
        }

        private async Task UploadLargeFileToPathAsync(string localFilePath, string remoteFilePath, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server and token settings first.");
            }

            if (string.IsNullOrWhiteSpace(remoteFilePath))
            {
                throw new InvalidOperationException("Remote upload path is required.");
            }

            await using var sourceStream = File.OpenRead(localFilePath);
            var encodedRemoteFilePath = EncodeRemotePath(remoteFilePath);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"{_settingsService.BaseUrl}/api/fs/put");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Headers.TryAddWithoutValidation("File-Path", encodedRemoteFilePath);

            var streamContent = new StreamContent(sourceStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            streamContent.Headers.ContentLength = sourceStream.Length;
            request.Content = streamContent;

            using var response = await _uploadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(GetErrorMessage(body, $"Upload failed with HTTP {(int)response.StatusCode}."));
            }

            var apiMessage = GetApiMessage(body);
            if (!string.IsNullOrWhiteSpace(apiMessage) &&
                !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(apiMessage);
            }
        }

        private async Task VerifyRemoteFileSizeWithRetryAsync(string remoteFilePath, long expectedSize, CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;
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
            var parentPath = GetRemoteParentPath(remoteFilePath);
            var fileName = GetRemoteFileName(remoteFilePath);
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

        private static string GetRemoteParentPath(string remotePath)
        {
            var normalized = remotePath.Replace('\\', '/').TrimEnd('/');
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash <= 0 ? "/" : normalized[..lastSlash];
        }

        private static string GetRemoteFileName(string remotePath)
        {
            var normalized = remotePath.Replace('\\', '/').TrimEnd('/');
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash < 0 ? normalized : normalized[(lastSlash + 1)..];
        }

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            return code == 502 || code == 503 || code == 504 || code == 429;
        }

        private static bool IsRetryableUploadException(Exception ex)
        {
            if (ex is InvalidOperationException invalidOperationException)
            {
                var message = invalidOperationException.Message;
                if (message.Contains("HTTP 502", StringComparison.Ordinal) ||
                    message.Contains("HTTP 503", StringComparison.Ordinal) ||
                    message.Contains("HTTP 504", StringComparison.Ordinal) ||
                    message.Contains("HTTP 429", StringComparison.Ordinal) ||
                    message.Contains("Upload size mismatch", StringComparison.Ordinal) ||
                    message.Contains("Upload stream ended early", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return ex is IOException or HttpRequestException;
        }

        private sealed class ProgressStreamContent : HttpContent
        {
            private readonly Stream _sourceStream;
            private readonly IProgress<double>? _progress;
            private readonly int _bufferSize;
            private readonly bool _disposeSource;
            private readonly long _startPosition;
            private readonly long _contentLength;

            public ProgressStreamContent(Stream sourceStream, IProgress<double>? progress, bool disposeSource = false, int bufferSize = 81_920)
            {
                _sourceStream = sourceStream;
                _progress = progress;
                _disposeSource = disposeSource;
                _bufferSize = bufferSize;

                if (_sourceStream.CanSeek)
                {
                    _startPosition = _sourceStream.Position;
                    _contentLength = _sourceStream.Length - _startPosition;
                    Headers.ContentLength = _contentLength;
                }
                else
                {
                    _startPosition = 0;
                    _contentLength = -1;
                }
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                if (_sourceStream.CanSeek)
                {
                    _sourceStream.Position = _startPosition;
                }

                var buffer = new byte[_bufferSize];
                long totalSent = 0;

                while (_contentLength < 0 || totalSent < _contentLength)
                {
                    var toRead = _contentLength < 0
                        ? buffer.Length
                        : (int)Math.Min(buffer.Length, _contentLength - totalSent);
                    if (toRead <= 0)
                    {
                        break;
                    }

                    var read = await _sourceStream.ReadAsync(buffer.AsMemory(0, toRead), CancellationToken.None);
                    if (read == 0)
                    {
                        break;
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None);
                    totalSent += read;

                    if (_progress != null && _contentLength > 0)
                    {
                        _progress.Report((double)totalSent / _contentLength);
                    }
                }

                if (_contentLength > 0 && totalSent != _contentLength)
                {
                    throw new InvalidOperationException(
                        $"Upload stream ended early. Expected {_contentLength} bytes, sent {totalSent} bytes.");
                }

                // 不在此处 Report(1.0)：还需等待 HTTP 响应与远端校验，由 UploadFileToPathAsync 在确认成功后上报。
            }

            protected override bool TryComputeLength(out long length)
            {
                if (_contentLength >= 0)
                {
                    length = _contentLength;
                    return true;
                }

                length = -1;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _disposeSource)
                {
                    _sourceStream.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
