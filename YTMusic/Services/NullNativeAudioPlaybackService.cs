using System;
using System.Threading.Tasks;

namespace YTMusic.Services
{
#pragma warning disable CS0067
    public class NullNativeAudioPlaybackService : INativeAudioPlaybackService
    {
        public bool IsSupported => false;
        public event Action<double, double>? PositionChanged;
        public event Action<bool>? PlayingStateChanged;
        public event Action? PlaybackEnded;

        public Task PlayAsync(string source, bool isLocalFile, string? title, string? artist) => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
        public Task SeekAsync(double positionSeconds) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
#pragma warning restore CS0067
}
