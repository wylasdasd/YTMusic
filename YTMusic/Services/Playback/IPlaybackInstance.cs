using System.Threading.Tasks;

namespace YTMusic.Services.Playback
{
    public interface IPlaybackInstance
    {
        PlaybackKind Kind { get; }

        Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options);
        Task DetachAsync(bool preserveNativeAudioBackend);
        Task PlayAsync();
        Task PauseAsync();
        Task SeekAsync(double positionSeconds);
    }
}
