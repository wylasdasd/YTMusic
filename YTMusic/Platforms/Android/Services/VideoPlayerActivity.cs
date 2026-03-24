using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.Res;
using Android.Views;
using Android.Widget;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;

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
        private static readonly float[] PlaybackSpeedOptions = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };

        private IExoPlayer? _player;
        private PlayerView? _playerView;
        private global::Android.Widget.Button? _settingsButton;
        private ControllerVisibilityListener? _controllerVisibilityListener;
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
            _settingsButton = new global::Android.Widget.Button(this)
            {
                LayoutParameters = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent,
                    GravityFlags.Bottom | GravityFlags.End)
                {
                    BottomMargin = DpToPx(56),
                    RightMargin = DpToPx(16)
                }
            };
            _settingsButton.SetAllCaps(false);
            _settingsButton.Alpha = 0.85f;
            _settingsButton.Text = "设置";
            _settingsButton.Click += OnSettingsButtonClick;

            root.AddView(_playerView);

            SetContentView(root);

            _player = new ExoPlayerBuilder(this).Build();
            _player.AddListener(this);
            _playerView.Player = _player;
            _controllerVisibilityListener = new ControllerVisibilityListener(this);
            _playerView.SetControllerVisibilityListener(_controllerVisibilityListener);
            _playerView.OverlayFrameLayout?.AddView(_settingsButton);
            TryHideDefaultSettingsButton();
            UpdateSettingsButtonVisibility(_playerView.IsControllerFullyVisible);

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
                _playerView.SetControllerVisibilityListener(null);
                _playerView.OverlayFrameLayout?.RemoveView(_settingsButton);
                _playerView.Player = null;
                _playerView.Dispose();
                _playerView = null;
            }

            if (_settingsButton != null)
            {
                _settingsButton.Click -= OnSettingsButtonClick;
                _settingsButton.Dispose();
                _settingsButton = null;
            }

            _mainHandler = null;
            _positionRunnable = null;
            _controllerVisibilityListener = null;
            _currentInstance = null;
            base.OnDestroy();
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            TryHideDefaultSettingsButton();
            UpdateSettingsButtonVisibility(_playerView?.IsControllerFullyVisible == true);
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

        private void OnSettingsButtonClick(object? sender, EventArgs e)
        {
            if (_settingsButton == null)
            {
                return;
            }

            var popupMenu = new PopupMenu(this, _settingsButton);
            popupMenu.Menu.Add(0, 1, 0, IsLandscape() ? "切换到竖屏" : "切换到横屏");
            popupMenu.Menu.Add(0, 2, 1, $"播放速度: {GetCurrentPlaybackSpeedLabel()}");
            popupMenu.MenuItemClick += OnSettingsMenuItemClick;
            popupMenu.DismissEvent += (_, _) =>
            {
                popupMenu.MenuItemClick -= OnSettingsMenuItemClick;
                popupMenu.Dispose();
            };
            popupMenu.Show();
        }

        private void OnSettingsMenuItemClick(object? sender, PopupMenu.MenuItemClickEventArgs e)
        {
            switch (e.Item.ItemId)
            {
                case 1:
                    ToggleOrientation();
                    break;
                case 2:
                    ShowPlaybackSpeedDialog();
                    break;
            }
        }

        private void ToggleOrientation()
        {
            RequestedOrientation = IsLandscape()
                ? global::Android.Content.PM.ScreenOrientation.Portrait
                : global::Android.Content.PM.ScreenOrientation.SensorLandscape;
        }

        private bool IsLandscape()
        {
            return Resources?.Configuration?.Orientation == global::Android.Content.Res.Orientation.Landscape;
        }

        private void ShowPlaybackSpeedDialog()
        {
            var labels = PlaybackSpeedOptions.Select(FormatPlaybackSpeedLabel).ToArray();
            var currentSpeed = GetCurrentPlaybackSpeed();
            var checkedIndex = Array.FindIndex(PlaybackSpeedOptions, speed => Math.Abs(speed - currentSpeed) < 0.01f);
            if (checkedIndex < 0)
            {
                checkedIndex = Array.FindIndex(PlaybackSpeedOptions, speed => Math.Abs(speed - 1f) < 0.01f);
            }

            var builder = new AlertDialog.Builder(this);
            builder.SetTitle("播放速度");
            builder.SetSingleChoiceItems(labels, checkedIndex, (_, args) =>
            {
                if (args.Which >= 0 && args.Which < PlaybackSpeedOptions.Length)
                {
                    SetPlaybackSpeed(PlaybackSpeedOptions[args.Which]);
                }
            });
            builder.SetNegativeButton("关闭", (_, _) => { });
            builder.Show();
        }

        private float GetCurrentPlaybackSpeed()
        {
            return _player?.PlaybackParameters?.Speed ?? 1f;
        }

        private string GetCurrentPlaybackSpeedLabel()
        {
            return FormatPlaybackSpeedLabel(GetCurrentPlaybackSpeed());
        }

        private static string FormatPlaybackSpeedLabel(float speed)
        {
            return $"{speed:0.##}x";
        }

        private void SetPlaybackSpeed(float speed)
        {
            _player?.SetPlaybackSpeed(speed);
        }

        private void UpdateSettingsButtonVisibility(bool isVisible)
        {
            if (_settingsButton == null)
            {
                return;
            }

            _settingsButton.Visibility = isVisible
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;
        }

        private void TryHideDefaultSettingsButton()
        {
            if (_playerView == null)
            {
                return;
            }

            HideViewByResourceEntryName(_playerView, "exo_settings");
        }

        private void HideViewByResourceEntryName(global::Android.Views.View view, string resourceEntryName)
        {
            if (view.Id != global::Android.Views.View.NoId)
            {
                try
                {
                    if (Resources?.GetResourceEntryName(view.Id) == resourceEntryName)
                    {
                        view.Visibility = global::Android.Views.ViewStates.Gone;
                    }
                }
                catch
                {
                    // Ignore views whose ids are not directly resolvable in the app resources.
                }
            }

            if (view is not global::Android.Views.ViewGroup viewGroup)
            {
                return;
            }

            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                var child = viewGroup.GetChildAt(i);
                if (child != null)
                {
                    HideViewByResourceEntryName(child, resourceEntryName);
                }
            }
        }

        private int DpToPx(int dp)
        {
            return (int)(dp * Resources!.DisplayMetrics!.Density);
        }

        private sealed class ControllerVisibilityListener : Java.Lang.Object, PlayerView.IControllerVisibilityListener
        {
            private readonly VideoPlayerActivity _activity;

            public ControllerVisibilityListener(VideoPlayerActivity activity)
            {
                _activity = activity;
            }

            public void OnVisibilityChanged(int visibility)
            {
                _activity.UpdateSettingsButtonVisibility(visibility == (int)global::Android.Views.ViewStates.Visible);
            }
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
