using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface ILocalMusicService
{
    Task<IReadOnlyList<DownloadedTrack>> GetDownloadedTracksAsync();
    Task<DownloadedTrack?> GetDownloadedTrackByVideoIdAsync(string videoId);
    Task<DownloadedTrack?> GetDownloadedTrackByFilePathAsync(string filePath);
    Task ResetAllAsync();
    Task AddDownloadedTrackAsync(DownloadedTrack track);
    Task RemoveDownloadedTrackAsync(string videoId, string filePath);
    Task MarkTrackUploadedAsync(string localFilePath, string remotePath);
    Task<DownloadedTrack?> GetDownloadedTrackByRemoteSourcePathAsync(string remoteSourcePath);

    Task<IReadOnlyList<LocalAudioFile>> GetDownloadedAudioFilesAsync();
    Task DeleteAudioFileAsync(string filePath);
}
