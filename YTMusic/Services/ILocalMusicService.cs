using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public interface ILocalMusicService
    {
        Task<IReadOnlyList<DownloadedTrack>> GetDownloadedTracksAsync();
        Task<DownloadedTrack?> GetDownloadedTrackByVideoIdAsync(string videoId);
        Task AddDownloadedTrackAsync(DownloadedTrack track);
        Task RemoveDownloadedTrackAsync(string videoId, string filePath);
        
        // Keep for legacy fallback or direct disk access if needed
        Task<IReadOnlyList<LocalAudioFile>> GetDownloadedAudioFilesAsync();
        Task DeleteAudioFileAsync(string filePath);
    }

    public class DownloadedTrack
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string LocalFilePath { get; set; } = string.Empty;
        public DateTime DownloadedDate { get; set; }
    }

    public class LocalAudioFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
    }
}
