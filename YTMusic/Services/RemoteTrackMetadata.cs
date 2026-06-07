using System;
using System.Text.Json.Serialization;

namespace YTMusic.Services
{
    /// <summary>
    /// 远端 metadata.json：对应 SQLite DownloadedTracks 的可同步字段子集（不含本地路径与上传状态）。
    /// </summary>
    public class RemoteTrackMetadata
    {
        public const string FileName = "metadata.json";

        public string VideoId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public bool IsVideo { get; set; }

        public DateTime DownloadedDate { get; set; }

        /// <summary>旧版 metadata 的 coverUrl，仅反序列化兼容。</summary>
        [JsonPropertyName("coverUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CoverUrl
        {
            get => null;
            set
            {
                if (string.IsNullOrWhiteSpace(ThumbnailUrl) && !string.IsNullOrWhiteSpace(value))
                {
                    ThumbnailUrl = value;
                }
            }
        }

        public static RemoteTrackMetadata FromDownloadedTrack(DownloadedTrack track)
        {
            return new RemoteTrackMetadata
            {
                VideoId = track.VideoId,
                Title = track.Title,
                Author = track.Author ?? string.Empty,
                ThumbnailUrl = track.ThumbnailUrl,
                IsVideo = track.IsVideo,
                DownloadedDate = track.DownloadedDate
            };
        }

        public DownloadedTrack ToDownloadedTrack(string localFilePath, string remoteMediaPath)
        {
            return new DownloadedTrack
            {
                VideoId = string.IsNullOrWhiteSpace(VideoId) ? $"alist:{remoteMediaPath}" : VideoId,
                Title = Title,
                Author = string.IsNullOrWhiteSpace(Author) ? "AList" : Author,
                ThumbnailUrl = ThumbnailUrl,
                LocalFilePath = localFilePath,
                IsVideo = IsVideo,
                DownloadedDate = DownloadedDate == default ? DateTime.UtcNow : DownloadedDate,
                HasUploaded = false,
                UploadedDate = null,
                UploadedRemotePath = null,
                RemoteSourcePath = remoteMediaPath
            };
        }
    }
}
