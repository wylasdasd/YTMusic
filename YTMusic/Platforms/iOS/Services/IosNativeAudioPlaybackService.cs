using System;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using Foundation;

namespace YTMusic.Platforms.iOS.Services
{
    public class IosNativeAudioPlaybackService : YTMusic.Services.INativeAudioPlaybackService
    {
        private AVPlayer? _player;
        private NSObject? _timeObserver;
        private NSObject? _endObserver;

        public bool IsSupported => true;
        public event Action<double, double>? PositionChanged;
        public event Action<bool>? PlayingStateChanged;
        public event Action? PlaybackEnded;

        public IosNativeAudioPlaybackService()
        {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback);
            session.SetActive(true);
        }

        public Task PlayAsync(string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null)
        {
            DisposePlayer();

            NSUrl url = isLocalFile ? NSUrl.FromFilename(source) : NSUrl.FromString(source)!;
            var item = AVPlayerItem.FromUrl(url);
            _player = new AVPlayer(item);

            _timeObserver = _player.AddPeriodicTimeObserver(
                CMTime.FromSeconds(0.5, 1000),
                null,
                _ =>
                {
                    if (_player?.CurrentItem == null)
                    {
                        return;
                    }

                    var current = _player.CurrentTime.Seconds;
                    var duration = _player.CurrentItem.Duration.Seconds;
                    if (double.IsNaN(current) || double.IsInfinity(current))
                    {
                        current = 0;
                    }

                    if (double.IsNaN(duration) || double.IsInfinity(duration))
                    {
                        duration = 0;
                    }

                    PositionChanged?.Invoke(current, duration);
                });

            _endObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                AVPlayerItem.DidPlayToEndTimeNotification,
                _ =>
                {
                    PlayingStateChanged?.Invoke(false);
                    PlaybackEnded?.Invoke();
                },
                item);

            _player.Play();
            PlayingStateChanged?.Invoke(true);
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            _player?.Pause();
            PlayingStateChanged?.Invoke(false);
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            _player?.Play();
            PlayingStateChanged?.Invoke(true);
            return Task.CompletedTask;
        }

        public Task SeekAsync(double positionSeconds)
        {
            _player?.Seek(CMTime.FromSeconds(Math.Max(0, positionSeconds), 1000));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            DisposePlayer();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposePlayer();
        }

        private void DisposePlayer()
        {
            if (_player == null)
            {
                return;
            }

            _player.Pause();

            if (_timeObserver != null)
            {
                _player.RemoveTimeObserver(_timeObserver);
                _timeObserver.Dispose();
                _timeObserver = null;
            }

            if (_endObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(_endObserver);
                _endObserver.Dispose();
                _endObserver = null;
            }

            _player.Dispose();
            _player = null;
        }
    }
}
