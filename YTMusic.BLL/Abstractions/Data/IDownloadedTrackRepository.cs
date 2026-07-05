using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions.Data;

public interface IDownloadedTrackRepository
{
    Task<IReadOnlyList<DownloadedTrack>> GetAllAsync();
    Task<DownloadedTrack?> GetByVideoIdAsync(string videoId);
    Task<DownloadedTrack?> GetByFilePathAsync(string filePath);
    Task<DownloadedTrack?> GetByRemoteSourcePathAsync(string remoteSourcePath);
    Task AddOrReplaceAsync(DownloadedTrack track);
    Task MarkUploadedAsync(string localFilePath, string remotePath, DateTime uploadedDate);
    Task DeleteByVideoIdAsync(string videoId);
    Task ResetAllAsync();
}
