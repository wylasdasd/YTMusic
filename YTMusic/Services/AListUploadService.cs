using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommonTool.FileHelps;

namespace YTMusic.Services
{
    public class AListDirectoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsDir { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    public class AListUploadService
    {
        private static readonly HttpClient _httpClient = new();
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
            var encodedRemoteFilePath = EncodeRemotePath(remoteFilePath);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"{_settingsService.BaseUrl}/api/fs/put");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Headers.TryAddWithoutValidation("File-Path", encodedRemoteFilePath);

            var sourceStream = File.OpenRead(localFilePath);
            request.Content = new ProgressStreamContent(sourceStream, progress);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

            return remoteFilePath;
        }

        public async Task<IReadOnlyList<AListDirectoryItem>> ListDirectoryFilesAsync(CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settingsService.BaseUrl}/api/fs/list");
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);
            request.Content = JsonContent.Create(new
            {
                path = _settingsService.RemoteDirectory,
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

                if (isDir)
                {
                    continue;
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
                    Path = CombineRemotePath(_settingsService.RemoteDirectory, name),
                    Size = size,
                    IsDir = false,
                    ModifiedAt = modifiedAt
                });
            }

            return result;
        }

        public async Task<string> DownloadFileAsync(string remotePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_settingsService.IsConfigured)
            {
                throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                throw new InvalidOperationException("Remote file path is required.");
            }

            var fileName = Path.GetFileName(remotePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Unable to determine remote file name.");
            }

            var localDirectory = StoragePaths.GetDownloadedMusicDirectory();
            FileHelp.EnsureDirectoryExists(localDirectory);
            var localFilePath = Path.Combine(localDirectory, FileHelp.SafeFileName(fileName));

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

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.TryAddWithoutValidation("Authorization", _settingsService.Token);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(GetErrorMessage(body, $"Download failed with HTTP {(int)response.StatusCode}."));
            }

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

        private sealed class ProgressStreamContent : HttpContent
        {
            private readonly Stream _sourceStream;
            private readonly IProgress<double>? _progress;
            private readonly int _bufferSize;

            public ProgressStreamContent(Stream sourceStream, IProgress<double>? progress, int bufferSize = 81_920)
            {
                _sourceStream = sourceStream;
                _progress = progress;
                _bufferSize = bufferSize;

                if (_sourceStream.CanSeek)
                {
                    Headers.ContentLength = _sourceStream.Length;
                }
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                var buffer = new byte[_bufferSize];
                long totalRead = 0;
                var totalLength = _sourceStream.CanSeek ? _sourceStream.Length : -1;

                int read;
                while ((read = await _sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await stream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (_progress != null && totalLength > 0)
                    {
                        _progress.Report((double)totalRead / totalLength);
                    }
                }

                _progress?.Report(1.0);
            }

            protected override bool TryComputeLength(out long length)
            {
                if (_sourceStream.CanSeek)
                {
                    length = _sourceStream.Length;
                    return true;
                }

                length = -1;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sourceStream.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
