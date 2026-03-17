using Android.Content;
using Android.Media;
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

        public Task PlayAsync(string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null)
        {
            var resolvedDuration = ResolveDurationSeconds(source, isLocalFile, durationSeconds);
            return PlaybackForegroundService.PlayAsync(_context, source, isLocalFile, title, artist, resolvedDuration);
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

        private static double? ResolveDurationSeconds(string source, bool isLocalFile, double? durationSeconds)
        {
            if (durationSeconds.HasValue && durationSeconds.Value > 0)
            {
                return durationSeconds;
            }

            if (!isLocalFile || string.IsNullOrWhiteSpace(source))
            {
                return durationSeconds;
            }

            try
            {
                using var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(source);
                var durationMsString = retriever.ExtractMetadata(MetadataKey.Duration);
                if (long.TryParse(durationMsString, out var durationMs) && durationMs > 0)
                {
                    return durationMs / 1000.0;
                }
            }
            catch
            {
                // Best-effort only.
            }

            return durationSeconds;
        }
    }
}
