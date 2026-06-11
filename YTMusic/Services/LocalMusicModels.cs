using System;

namespace YTMusic.Services
{
    public class DownloadedTrack
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string LocalFilePath { get; set; } = string.Empty;
        public bool IsVideo { get; set; }
        public DateTime DownloadedDate { get; set; }
        public bool HasUploaded { get; set; }
        public DateTime? UploadedDate { get; set; }
        public string? UploadedRemotePath { get; set; }
        public string? RemoteSourcePath { get; set; }
    }

    public class LocalAudioFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
    }
}
