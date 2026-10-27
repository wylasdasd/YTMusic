using YTMusic.Services.Abstractions;

namespace YTMusic.Services.Playback;

/// <summary>
/// Pure routing helpers for local vs native vs web playback activation.
/// </summary>
public static class PlaybackRouteSelector
{
    /// <summary>
    /// Local files only play as video when marked as video and extension is .mp4; webm etc. stay audio.
    /// </summary>
    public static bool ShouldPlayLocalAsVideo(string filePath, bool markedAsVideo)
    {
        if (!markedAsVideo)
        {
            return false;
        }

        return Path.GetExtension(filePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldUseNativeLocalVideo(
        INativeVideoPlaybackService nativeVideo,
        string filePath,
        bool markedAsVideo)
        => OperatingSystem.IsAndroid()
           && nativeVideo.IsSupported
           && ShouldPlayLocalAsVideo(filePath, markedAsVideo);

    public static bool ShouldUseNativeOnlineVideo(INativeVideoPlaybackService nativeVideo)
        => OperatingSystem.IsAndroid() && nativeVideo.IsSupported;

    public static PlaybackOptions BuildPlaybackOptions(bool autoPlay) => new() { AutoPlay = autoPlay };

    public static PlaybackSource BuildLocalPlaybackSource(
        string filePath,
        bool isWebM,
        bool isVideo,
        string? title,
        string? artist,
        double? durationSeconds,
        string? thumbnailUrl = null) => new()
    {
        LocalFilePath = filePath,
        IsLocalFile = true,
        IsWebM = isWebM,
        IsVideo = isVideo,
        Title = title,
        Artist = artist,
        DurationSeconds = durationSeconds,
        ThumbnailUrl = thumbnailUrl
    };

    public static PlaybackSource BuildWebPlaybackSource(
        string streamUrl,
        bool isWebM,
        bool isVideo,
        string? title,
        string? artist,
        double? durationSeconds) => new()
    {
        StreamUrl = streamUrl,
        IsLocalFile = false,
        IsWebM = isWebM,
        IsVideo = isVideo,
        Title = title,
        Artist = artist,
        DurationSeconds = durationSeconds
    };

    /// <summary>
    /// Chooses NativeVideo / NativeAudio / WebMuxedVideo / WebAudio for a local file path.
    /// </summary>
    public static PlaybackKind SelectLocalKind(
        INativeAudioPlaybackService nativeAudio,
        INativeVideoPlaybackService nativeVideo,
        string filePath,
        bool markedAsVideo)
    {
        var isStreamVideo = ShouldPlayLocalAsVideo(filePath, markedAsVideo);
        if (ShouldUseNativeLocalVideo(nativeVideo, filePath, markedAsVideo))
        {
            return PlaybackKind.NativeVideo;
        }

        if (nativeAudio.IsSupported && !isStreamVideo)
        {
            return PlaybackKind.NativeAudio;
        }

        return isStreamVideo ? PlaybackKind.WebMuxedVideo : PlaybackKind.WebAudio;
    }
}
