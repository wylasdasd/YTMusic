using YoutubeExplode.Videos.Streams;

namespace YTMusic.Services.Playback;

public sealed class PlayingItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? LocalFilePath { get; set; }
    public bool? IsVideo { get; set; }
    public double? DurationSeconds { get; set; }
}

public sealed class RemoteWebVideoStreams
{
    public IStreamInfo VideoStream { get; init; } = null!;
    public IStreamInfo? CompanionAudioStream { get; init; }
}
