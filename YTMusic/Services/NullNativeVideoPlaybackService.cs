using System;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class NullNativeVideoPlaybackService : INativeVideoPlaybackService
    {
        public bool IsSupported => false;
        public event Action<double, double>? PositionChanged { add { } remove { } }
        public event Action<bool>? PlayingStateChanged { add { } remove { } }
        public event Action? PlaybackEnded { add { } remove { } }
        public event Action? PlaybackStopped { add { } remove { } }

        public Task PlayAsync(string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null) => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
        public Task SeekAsync(double positionSeconds) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
