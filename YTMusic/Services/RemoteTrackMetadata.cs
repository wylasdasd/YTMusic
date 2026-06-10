using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CommonTool.FileHelps;

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

        /// <summary>
        /// 歌曲所在收藏夹名称（跨设备同步用名称而非 Id）；下载时用于恢复收藏夹并可选落盘子目录。
        /// </summary>
        public List<string> FavoriteFolderNames { get; set; } = new();

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

        public string? GetPrimaryFolderName()
        {
            return FavoriteFolderNames
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?
                .Trim();
        }

        public static string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames)
        {
            var primary = folderNames?
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?
                .Trim();

            var baseDir = StoragePaths.GetDownloadedMusicDirectory();
            if (string.IsNullOrWhiteSpace(primary))
            {
                return baseDir;
            }

            return Path.Combine(baseDir, FileHelp.SafeFileName(primary));
        }
    }
}
