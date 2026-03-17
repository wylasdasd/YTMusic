using Android.Content;
using Application = Android.App.Application;

namespace YTMusic.Platforms.Android.Services
{
    public class AndroidNativeAudioPlaybackService : YTMusic.Services.INativeAudioPlaybackService
    {
        private readonly Context _context;

        public bool IsSupported => true;

        public event Action<double, double>? PositionChanged;
        public event Action<bool>? PlayingStateChanged;
        public event Action? PlaybackEnded;

        public AndroidNativeAudioPlaybackService()
        {
            _context = Application.Context;
            PlaybackForegroundService.PositionChanged += OnPositionChanged;
            PlaybackForegroundService.PlayingStateChanged += OnPlayingStateChanged;
            PlaybackForegroundService.PlaybackEnded += OnPlaybackEnded;
        }

        public Task PlayAsync(string source, bool isLocalFile, string? title, string? artist)
        {
            return PlaybackForegroundService.PlayAsync(_context, source, isLocalFile, title, artist);
        }

        public Task PauseAsync()
        {
            return PlaybackForegroundService.PauseAsync(_context);
        }

        public Task ResumeAsync()
        {
            return PlaybackForegroundService.ResumeAsync(_context);
        }

        public Task SeekAsync(double positionSeconds)
        {
            var ms = (long)Math.Max(0, positionSeconds * 1000);
            return PlaybackForegroundService.SeekAsync(_context, ms);
        }

        public Task StopAsync()
        {
            return PlaybackForegroundService.StopAsync(_context);
        }

        public void Dispose()
        {
            PlaybackForegroundService.PositionChanged -= OnPositionChanged;
            PlaybackForegroundService.PlayingStateChanged -= OnPlayingStateChanged;
            PlaybackForegroundService.PlaybackEnded -= OnPlaybackEnded;
        }

        private void OnPositionChanged(double current, double duration)
        {
            PositionChanged?.Invoke(current, duration);
        }

        private void OnPlayingStateChanged(bool isPlaying)
        {
            PlayingStateChanged?.Invoke(isPlaying);
        }

        private void OnPlaybackEnded()
        {
            PlaybackEnded?.Invoke();
        }
    }
}
