using System.Threading.Tasks;
using YoutubeExplode.Videos.Streams;
using YTMusic.Services.Abstractions;
using YTMusic.Services.Playback;

namespace YTMusic.Services.Abstractions.Playback
{
    public interface IPlaybackHost
    {
        INativeAudioPlaybackService NativeAudio { get; }
        INativeVideoPlaybackService NativeVideo { get; }

        void SetActivePlaybackKind(PlaybackKind kind);
        void UpdateStreamPresentation(string? streamUrl, bool isWebM, bool isVideo);
        void ResetPlaybackTiming();
        void NotifyStateChanged();

        Task StopWebPlaybackAsync();
        void RequestWebStateSync();
        Task PauseWebPlaybackAsync();
        Task PlayWebPlaybackAsync(bool videoOnly);
        Task SeekWebPlaybackAsync(double positionSeconds);

        Task<string> BuildWebVideoStreamUrlAsync(IStreamInfo videoStreamInfo);
        Task EnsureFileProxyCreatedAsync();
        Task EnsureAudioProxyCreatedAsync();
        void ConfigureFileProxy(string filePath, bool isVideo);
        void ConfigureAudioProxy(IStreamInfo streamInfo, bool isVideo);
        string BuildLocalProxyStreamUrl(string localFilePath);
    }
}
