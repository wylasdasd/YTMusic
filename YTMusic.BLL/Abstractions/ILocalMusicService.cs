using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface ILocalMusicService
{
    Task<IReadOnlyList<DownloadedTrack>> GetDownloadedTracksAsync();
    /// <summary>下载记录（库表全量，不因本地文件缺失而删行）。</summary>
    Task<IReadOnlyList<DownloadedTrack>> GetDownloadRecordsAsync();
    Task<DownloadedTrack?> GetDownloadedTrackByVideoIdAsync(string videoId);
    /// <summary>按 VideoId 读取下载登记，不因本地文件缺失而删库。</summary>
    Task<DownloadedTrack?> GetDownloadedTrackRecordAsync(string videoId);
    Task<IReadOnlyDictionary<string, DownloadedTrack>> GetDownloadedTracksByVideoIdsAsync(IEnumerable<string> videoIds);
    Task<DownloadedTrack?> GetDownloadedTrackByFilePathAsync(string filePath);
    Task ResetAllAsync();
    Task AddDownloadedTrackAsync(DownloadedTrack track);
    /// <summary>YouTube/AList 下载任务成功后写入下载记录库并同步收藏本地路径。</summary>
    Task SaveCompletedDownloadRecordAsync(
        string videoId,
        string title,
        string localFilePath,
        bool isVideo,
        string? remoteSourcePath = null);
    Task RemoveDownloadedTrackAsync(string videoId, string filePath);
    /// <summary>删除本地文件并清空登记路径，保留下载记录行。</summary>
    Task ClearDownloadedLocalFileAsync(string videoId, string? filePath);
    Task MarkTrackUploadedAsync(string localFilePath, string remotePath);
    Task<DownloadedTrack?> GetDownloadedTrackByRemoteSourcePathAsync(string remoteSourcePath);

    Task<IReadOnlyList<LocalAudioFile>> GetDownloadedAudioFilesAsync();
    Task DeleteAudioFileAsync(string filePath, string? videoId = null);
}
