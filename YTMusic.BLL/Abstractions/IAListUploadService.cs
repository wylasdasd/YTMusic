using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IAListUploadService
{
    Task CreateDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AListDirectoryItem>> ListDirectoryItemsAsync(string remoteDirectory, CancellationToken cancellationToken = default);
    Task<T?> TryDownloadJsonAsync<T>(string remotePath, CancellationToken cancellationToken = default);
}
