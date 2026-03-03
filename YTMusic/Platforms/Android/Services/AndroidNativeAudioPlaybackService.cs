using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Application = Android.App.Application;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Common;
using Microsoft.Maui.ApplicationModel;
using Object = Java.Lang.Object;

namespace YTMusic.Platforms.Android.Services
{
    public class AndroidNativeAudioPlaybackService : Object, YTMusic.Services.INativeAudioPlaybackService, IPlayerListener
    {
        private const int PlaybackStateEnded = 4; // Media3 Player.STATE_ENDED
        private readonly Context _context;
        private IExoPlayer? _player;
        private Timer? _positionTimer;
        private bool _foregroundServiceStarted;

        public bool IsSupported => true;

        public event Action<double, double>? PositionChanged;

        public event Action<bool>? PlayingStateChanged;

        public event Action? PlaybackEnded;

        public AndroidNativeAudioPlaybackService()
        {
            _context = Application.Context;
        }

        public async Task PlayAsync(string source, bool isLocalFile, string? title, string? artist)
        {
            await StopAsync(); // Clean up previous player before creating a new one
            await RunOnMainThreadAsync(() =>
            {
                EnsurePlayer();

                var player = _player!;
                var mediaItem = MediaItem.FromUri(source);
                player.SetMediaItem(mediaItem);
                player.Prepare();
                player.Play();
                StartTimer();
            });

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                PlaybackForegroundService.Start(_context, title, artist);
            }
            else
            {
                var intent = new Intent(_context, typeof(PlaybackForegroundService));
                intent.SetAction("YTMusic.Playback.Start");
                intent.PutExtra("title", title ?? "YTMusic");
                intent.PutExtra("artist", artist ?? "Playing");
                _context.StartService(intent);
            }

            _foregroundServiceStarted = true;
        }

        public Task PauseAsync()
        {
            return RunOnMainThreadAsync(() =>
            {
                _player?.Pause();
            });
        }

        public Task ResumeAsync()
        {
            return RunOnMainThreadAsync(() =>
            {
                _player?.Play();
                StartTimer();
            });
        }

        public Task SeekAsync(double positionSeconds)
        {
            return RunOnMainThreadAsync(() =>
            {
                _player?.SeekTo((long)(positionSeconds * 1000));
            });
        }

        public async Task StopAsync()
        {
            StopTimer();

            await RunOnMainThreadAsync(() =>
            {
                if (_player == null)
                {
                    return;
                }

                _player.Stop();
                _player.RemoveListener(this);
                _player.Release();
                _player.Dispose();
                _player = null;
            });

            if (_foregroundServiceStarted)
            {
                PlaybackForegroundService.Stop(_context);
                _foregroundServiceStarted = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ = StopAsync();
            }
            base.Dispose(disposing);
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                return;
            }

            _player = new ExoPlayerBuilder(_context).Build();
            _player.AddListener(this);
        }

        private void StartTimer()
        {
            StopTimer();
            _positionTimer = new Timer(_ =>
            {
                _ = RunOnMainThreadAsync(() =>
                {
                    var player = _player;
                    if (player == null || !player.IsPlaying)
                    {
                        return;
                    }

                    try
                    {
                        var current = player.CurrentPosition / 1000.0;
                        var duration = Math.Max(0, player.Duration) / 1000.0;
                        PositionChanged?.Invoke(current, duration);
                    }
                    catch { }
                });
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        private void StopTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        private static Task RunOnMainThreadAsync(Action action)
        {
            if (MainThread.IsMainThread)
            {
                action();
                return Task.CompletedTask;
            }

            return MainThread.InvokeOnMainThreadAsync(action);
        }

        public void OnIsPlayingChanged(bool isPlaying)
        {
            PlayingStateChanged?.Invoke(isPlaying);
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == PlaybackStateEnded)
            {
                StopTimer();
                PlayingStateChanged?.Invoke(false);
                PlaybackEnded?.Invoke();
            }
        }
    }
}
