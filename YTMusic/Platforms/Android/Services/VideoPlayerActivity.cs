using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using Java.Lang;

namespace YTMusic.Platforms.Android.Services
{
    [Activity(
        Exported = false,
        Theme = "@style/Maui.MainTheme",
        LaunchMode = global::Android.Content.PM.LaunchMode.SingleTask,
        ConfigurationChanges = global::Android.Content.PM.ConfigChanges.ScreenSize
            | global::Android.Content.PM.ConfigChanges.Orientation
            | global::Android.Content.PM.ConfigChanges.UiMode
            | global::Android.Content.PM.ConfigChanges.ScreenLayout
            | global::Android.Content.PM.ConfigChanges.SmallestScreenSize
            | global::Android.Content.PM.ConfigChanges.Density)]
    public class VideoPlayerActivity : Activity, IPlayerListener
    {
        private const int PlaybackStateEnded = 4; // Player.STATE_ENDED
        private const string ActionPlaySource = "YTMusic.Video.PlaySource";
        private const string ActionPause = "YTMusic.Video.Pause";
        private const string ActionResume = "YTMusic.Video.Resume";
        private const string ActionSeek = "YTMusic.Video.Seek";
        private const string ActionStop = "YTMusic.Video.Stop";
        private const string ExtraSource = "source";
        private const string ExtraIsLocalFile = "isLocalFile";
        private const string ExtraSeekMs = "seekMs";
        private const string ExtraDurationMs = "durationMs";

        private static WeakReference<VideoPlayerActivity>? _currentInstance;

        private IExoPlayer? _player;
        private PlayerView? _playerView;
        private Handler? _mainHandler;
        private global::Java.Lang.Runnable? _positionRunnable;
        private long _expectedDurationMs;

        public static event Action<double, double>? PositionChanged;

        public static event Action<bool>? PlayingStateChanged;

        public static event Action? PlaybackEnded;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _currentInstance = new WeakReference<VideoPlayerActivity>(this);

            _mainHandler = new Handler(Looper.MainLooper!);

            var root = new FrameLayout(this)
            {
                LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
            };
            root.SetBackgroundColor(global::Android.Graphics.Color.Black);

            _playerView = new PlayerView(this)
            {
                LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
            };
            _playerView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            _playerView.UseController = true;
            root.AddView(_playerView);

            SetContentView(root);

            _player = new ExoPlayerBuilder(this).Build();
            _player.AddListener(this);
            _playerView.Player = _player;

            HandleIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

        protected override void OnDestroy()
        {
            StopPositionUpdates();

            if (_player != null)
            {
                _player.RemoveListener(this);
                _player.Release();
                _player.Dispose();
                _player = null;
            }

            if (_playerView != null)
            {
                _playerView.Player = null;
                _playerView.Dispose();
                _playerView = null;
            }

            _mainHandler = null;
            _positionRunnable = null;
            _currentInstance = null;
            base.OnDestroy();
        }

        public void OnIsPlayingChanged(bool isPlaying)
        {
            PlayingStateChanged?.Invoke(isPlaying);
            if (isPlaying)
            {
                StartPositionUpdates();
            }
            else
            {
                StopPositionUpdates();
            }
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == PlaybackStateEnded)
            {
                StopPositionUpdates();
                PlayingStateChanged?.Invoke(false);
                PlaybackEnded?.Invoke();
            }
        }

        public static Task PlayAsync(Context context, string source, bool isLocalFile, string? title, string? artist, double? durationSeconds)
        {
            var intent = CreateIntent(context, ActionPlaySource);
            intent.PutExtra(ExtraSource, source);
            intent.PutExtra(ExtraIsLocalFile, isLocalFile);
            if (durationSeconds.HasValue && durationSeconds.Value > 0)
            {
                intent.PutExtra(ExtraDurationMs, (long)(durationSeconds.Value * 1000));
            }

            context.StartActivity(intent);
            return Task.CompletedTask;
        }

        public static Task PauseAsync(Context context)
        {
            return DispatchOrLaunchAsync(context, ActionPause);
        }

        public static Task ResumeAsync(Context context)
        {
            return DispatchOrLaunchAsync(context, ActionResume);
        }

        public static Task SeekAsync(Context context, long positionMs)
        {
            if (_currentInstance != null && _currentInstance.TryGetTarget(out var activity))
            {
                activity.RunOnUiThread(() => activity._player?.SeekTo(System.Math.Max(0, positionMs)));
                return Task.CompletedTask;
            }

            var intent = CreateIntent(context, ActionSeek);
            intent.PutExtra(ExtraSeekMs, System.Math.Max(0, positionMs));
            context.StartActivity(intent);
            return Task.CompletedTask;
        }

        public static Task StopAsync(Context context)
        {
            if (_currentInstance != null && _currentInstance.TryGetTarget(out var activity))
            {
                activity.RunOnUiThread(() =>
                {
                    activity._player?.Stop();
                    activity.Finish();
                });
                return Task.CompletedTask;
            }

            context.StartActivity(CreateIntent(context, ActionStop));
            return Task.CompletedTask;
        }

        private static Task DispatchOrLaunchAsync(Context context, string action)
        {
            if (_currentInstance != null && _currentInstance.TryGetTarget(out var activity))
            {
                activity.RunOnUiThread(() =>
                {
                    if (action == ActionPause)
                    {
                        activity._player?.Pause();
                    }
                    else if (action == ActionResume)
                    {
                        activity._player?.Play();
                    }
                });
                return Task.CompletedTask;
            }

            context.StartActivity(CreateIntent(context, action));
            return Task.CompletedTask;
        }

        private void HandleIntent(Intent? intent)
        {
            if (_player == null || intent?.Action == null)
            {
                return;
            }

            switch (intent.Action)
            {
                case ActionPlaySource:
                    {
                        var source = intent.GetStringExtra(ExtraSource);
                        if (string.IsNullOrWhiteSpace(source))
                        {
                            return;
                        }

                        var isLocalFile = intent.GetBooleanExtra(ExtraIsLocalFile, false);
                        _expectedDurationMs = System.Math.Max(0, intent.GetLongExtra(ExtraDurationMs, 0));

                        var mediaItem = new MediaItem.Builder()
                            .SetUri(ResolveUri(source, isLocalFile))
                            .Build();

                        _player.SetMediaItem(mediaItem);
                        _player.Prepare();
                        _player.Play();
                        StartPositionUpdates();
                        break;
                    }
                case ActionPause:
                    _player.Pause();
                    break;

                case ActionResume:
                    _player.Play();
                    break;

                case ActionSeek:
                    {
                        var seekMs = intent.GetLongExtra(ExtraSeekMs, -1);
                        if (seekMs >= 0)
                        {
                            _player.SeekTo(seekMs);
                        }
                        break;
                    }
                case ActionStop:
                    _player.Stop();
                    Finish();
                    break;
            }
        }

        private void StartPositionUpdates()
        {
            if (_mainHandler == null || _positionRunnable != null)
            {
                return;
            }

            _positionRunnable = new global::Java.Lang.Runnable(() =>
            {
                var player = _player;
                if (player == null)
                {
                    return;
                }

                var currentSeconds = System.Math.Max(0, player.CurrentPosition) / 1000.0;
                var durationMs = System.Math.Max(0, player.Duration);
                if (durationMs <= 0 && _expectedDurationMs > 0)
                {
                    durationMs = _expectedDurationMs;
                }

                PositionChanged?.Invoke(currentSeconds, System.Math.Max(0, durationMs) / 1000.0);
                _mainHandler?.PostDelayed(_positionRunnable!, 1000);
            });

            _mainHandler.Post(_positionRunnable);
        }

        private void StopPositionUpdates()
        {
            if (_mainHandler != null && _positionRunnable != null)
            {
                _mainHandler.RemoveCallbacks(_positionRunnable);
            }

            _positionRunnable = null;
        }

        private static Intent CreateIntent(Context context, string action)
        {
            var intent = new Intent(context, typeof(VideoPlayerActivity));
            intent.SetAction(action);
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            return intent;
        }

        private static global::Android.Net.Uri ResolveUri(string source, bool isLocalFile)
        {
            if (!isLocalFile)
            {
                return global::Android.Net.Uri.Parse(source)!;
            }

            if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return global::Android.Net.Uri.Parse(source)!;
            }

            var file = new Java.IO.File(source);
            return global::Android.Net.Uri.FromFile(file)!;
        }
    }
}