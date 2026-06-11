using YoutubeExplode.Videos.Streams;

namespace YTMusic.Services.Playback
{
    public enum PlaybackKind
    {
        None = 0,
        NativeAudio,
        NativeVideo,
        WebAudio,
        WebMuxedVideo,
        Hybrid
    }

    public sealed class PlaybackSource
    {
        public string? StreamUrl { get; init; }
        public string? LocalFilePath { get; init; }
        public bool IsLocalFile { get; init; }
        public bool IsWebM { get; init; }
        public bool IsVideo { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public double? DurationSeconds { get; init; }
        public string? CompanionAudioUrl { get; init; }
        public IStreamInfo? ProxyStreamInfo { get; init; }
    }

    public sealed class PlaybackOptions
    {
        public bool AutoPlay { get; init; } = true;
    }

    public static class PlaybackKindExtensions
    {
        public static bool SharesNativeAudioBackend(this PlaybackKind from, PlaybackKind to)
        {
            return from is PlaybackKind.NativeAudio or PlaybackKind.Hybrid
                && to is PlaybackKind.NativeAudio or PlaybackKind.Hybrid;
        }

        public static bool UsesWebSink(this PlaybackKind kind)
        {
            return kind is PlaybackKind.WebAudio or PlaybackKind.WebMuxedVideo or PlaybackKind.Hybrid;
        }
    }
}
