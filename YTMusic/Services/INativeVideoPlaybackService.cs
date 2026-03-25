using System;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public interface INativeVideoPlaybackService : IDisposable
    {
        bool IsSupported { get; }
        event Action<double, double>? PositionChanged;
        event Action<bool>? PlayingStateChanged;
        event Action? PlaybackEnded;
        event Action? PlaybackStopped;

        Task PlayAsync(string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null);
        Task PauseAsync();
        Task ResumeAsync();
        Task SeekAsync(double positionSeconds);
        Task StopAsync();
    }
}
