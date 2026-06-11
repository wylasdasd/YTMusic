using System;
using System.Threading.Tasks;

namespace YTMusic.Services.Abstractions
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
        /// <summary>释放当前输出但保留原生播放器会话（换源时避免 StopSelf 竞态）。</summary>
        Task DetachAsync();
    }
}
