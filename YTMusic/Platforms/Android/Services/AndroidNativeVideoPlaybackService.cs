using Android.Content;
using Application = Android.App.Application;
using Microsoft.Maui.ApplicationModel;

namespace YTMusic.Platforms.Android.Services
{
    public class AndroidNativeVideoPlaybackService : YTMusic.Services.Abstractions.INativeVideoPlaybackService
    {
        private readonly Context _context;

        public bool IsSupported => true;

        public event Action<double, double>? PositionChanged;
        public event Action<bool>? PlayingStateChanged;
        public event Action? PlaybackEnded;
        public event Action? PlaybackStopped;

        public AndroidNativeVideoPlaybackService()
        {
            _context = Application.Context;
            VideoPlayerActivity.PositionChanged += OnPositionChanged;
            VideoPlayerActivity.PlayingStateChanged += OnPlayingStateChanged;
            VideoPlayerActivity.PlaybackEnded += OnPlaybackEnded;
            VideoPlayerActivity.PlaybackStopped += OnPlaybackStopped;
        }

        public async Task PlayAsync(
            string source,
            bool isLocalFile,
            string? title,
            string? artist,
            double? durationSeconds = null,
            string? companionAudioUrl = null,
            bool autoPlay = true)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var context = Platform.CurrentActivity ?? _context;
                VideoPlayerActivity.PlayAsync(
                    context, source, isLocalFile, title, artist, durationSeconds, companionAudioUrl, autoPlay);
            }).ConfigureAwait(false);
        }

        public Task PauseAsync()
        {
            return VideoPlayerActivity.PauseAsync(_context);
        }

        public Task ResumeAsync()
        {
            return VideoPlayerActivity.ResumeAsync(_context);
        }

        public Task SeekAsync(double positionSeconds)
        {
            var ms = (long)Math.Max(0, positionSeconds * 1000);
            return VideoPlayerActivity.SeekAsync(_context, ms);
        }

        public Task StopAsync()
        {
            return VideoPlayerActivity.StopAsync(_context);
        }

        public void Dispose()
        {
            VideoPlayerActivity.PositionChanged -= OnPositionChanged;
            VideoPlayerActivity.PlayingStateChanged -= OnPlayingStateChanged;
            VideoPlayerActivity.PlaybackEnded -= OnPlaybackEnded;
            VideoPlayerActivity.PlaybackStopped -= OnPlaybackStopped;
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

        private void OnPlaybackStopped()
        {
            PlaybackStopped?.Invoke();
        }
    }
}
