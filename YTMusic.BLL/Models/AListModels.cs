using System.Text.Json.Serialization;

namespace YTMusic.BLL.Models;

public class AListDirectoryItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDir { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class AListBrowserItem
{
    public required AListDirectoryItem Item { get; init; }
    public required string DisplayName { get; init; }
    public string? Subtitle { get; init; }
    public string? ThumbnailUrl { get; init; }
}

/// <summary>
/// 远端 metadata.json：对应 SQLite DownloadedTracks 的可同步字段子集。
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
    public List<string> FavoriteFolderNames { get; set; } = new();

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
}

public static class AListPathHelper
{
    public static string NormalizeBaseUrl(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.TrimEnd('/');
    }

    public static string NormalizeDirectory(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        while (trimmed.Contains("//", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
        }

        if (trimmed.Length > 1)
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }
}
