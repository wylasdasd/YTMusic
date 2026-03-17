using System;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public interface INativeAudioPlaybackService : IDisposable
    {
        bool IsSupported { get; }
        event Action<double, double>? PositionChanged;
        event Action<bool>? PlayingStateChanged;
        event Action? PlaybackEnded;

        Task PlayAsync(string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null);
        Task PauseAsync();
        Task ResumeAsync();
        Task SeekAsync(double positionSeconds);
        Task StopAsync();
    }
}
