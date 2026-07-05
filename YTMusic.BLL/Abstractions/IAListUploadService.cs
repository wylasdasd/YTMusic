using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IAListUploadService
{
    Task<string> UploadFileAsync(string localFilePath, string? displayName, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> UploadFileToPathAsync(string localFilePath, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string remoteDirectoryPath, CancellationToken cancellationToken = default);
    Task<string> UploadCoverFromUrlAsync(string coverUrl, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> UploadCoverAsync(string coverSource, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> UploadJsonToPathAsync<T>(T payload, string remoteFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<T?> TryDownloadJsonAsync<T>(string remotePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AListDirectoryItem>> ListDirectoryItemsAsync(string? remotePath = null, CancellationToken cancellationToken = default);
    Task<string> DownloadFileAsync(string remotePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> DownloadFileToPathAsync(string remotePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
