namespace YTMusic.BLL.Models;

public sealed class PlaybackHistoryRecord
{
    public long Id { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? LocalFilePath { get; set; }
    public bool IsVideo { get; set; }
    public double? DurationSeconds { get; set; }
    public DateTime PlayedAtUtc { get; set; }
}
