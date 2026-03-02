using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Android.OS;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace YTMusic.Platforms.Android.Services
{
    public class AndroidNativeAudioPlaybackService : YTMusic.Services.INativeAudioPlaybackService
    {
        private readonly Context _context;
        private MediaPlayer? _player;
        private Timer? _positionTimer;
        private bool _isPrepared;

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
            await StopAsync();
            EnsurePlayer();

            var player = _player!;
            var preparedTcs = new TaskCompletionSource<bool>();

            player.Prepared += OnPrepared;
            player.Completion += OnCompletion;
            player.Error += OnError;

            void OnPrepared(object? sender, EventArgs args)
            {
                _isPrepared = true;
                preparedTcs.TrySetResult(true);
            }

            void OnCompletion(object? sender, EventArgs args)
            {
                StopTimer();
                PlayingStateChanged?.Invoke(false);
                PlaybackEnded?.Invoke();
            }

            void OnError(object? sender, MediaPlayer.ErrorEventArgs args)
            {
                StopTimer();
                _isPrepared = false;
                preparedTcs.TrySetException(new InvalidOperationException($"Android MediaPlayer error: {args.What}/{args.Extra}"));
            }

            player.Reset();
            _isPrepared = false;

            if (isLocalFile)
            {
                player.SetDataSource(source);
            }
            else
            {
                player.SetDataSource(_context, Uri.Parse(source));
            }

            player.PrepareAsync();
            await preparedTcs.Task;

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

            player.Start();
            PlayingStateChanged?.Invoke(true);
            StartTimer();
        }

        public Task PauseAsync()
        {
            if (_player?.IsPlaying == true)
            {
                _player.Pause();
                PlayingStateChanged?.Invoke(false);
            }

            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            if (_player != null && _isPrepared && !_player.IsPlaying)
            {
                _player.Start();
                PlayingStateChanged?.Invoke(true);
                StartTimer();
            }

            return Task.CompletedTask;
        }

        public Task SeekAsync(double positionSeconds)
        {
            if (_player != null && _isPrepared)
            {
                var positionMs = (int)Math.Max(0, positionSeconds * 1000);
                _player.SeekTo(positionMs);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopTimer();
            if (_player != null)
            {
                try
                {
                    if (_player.IsPlaying)
                    {
                        _player.Stop();
                    }
                }
                catch { }

                _player.Release();
                _player.Dispose();
                _player = null;
            }

            _isPrepared = false;
            PlaybackForegroundService.Stop(_context);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = StopAsync();
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                return;
            }

            _player = new MediaPlayer();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                var attributes = new AudioAttributes.Builder()
                    .SetContentType(AudioContentType.Music)
                    .SetUsage(AudioUsageKind.Media)
                    .Build();
                _player.SetAudioAttributes(attributes);
            }
            else
            {
                _player.SetAudioStreamType(global::Android.Media.Stream.Music);
            }
        }

        private void StartTimer()
        {
            StopTimer();
            _positionTimer = new Timer(_ =>
            {
                var player = _player;
                if (player == null || !_isPrepared)
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
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        private void StopTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = null;
        }
    }
}
