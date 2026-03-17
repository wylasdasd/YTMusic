using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;

namespace YTMusic.Platforms.Android.Services
{
    [Service(
        Exported = false,
        ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
    [IntentFilter(new[] { "androidx.media3.session.MediaSessionService" })]
    public class PlaybackForegroundService : MediaSessionService, IPlayerListener
    {
        private const int PlaybackStateEnded = 4; // Player.STATE_ENDED
        private const string NotificationChannelId = "ytmusic_playback_channel_v2";
        private const int NotificationId = 20260317;

        private const string ActionPlaySource = "YTMusic.Playback.PlaySource";
        private const string ActionPause = "YTMusic.Playback.Pause";
        private const string ActionResume = "YTMusic.Playback.Resume";
        private const string ActionSeek = "YTMusic.Playback.Seek";
        private const string ActionStop = "YTMusic.Playback.Stop";

        private const string ExtraSource = "source";
        private const string ExtraIsLocalFile = "isLocalFile";
        private const string ExtraTitle = "title";
        private const string ExtraArtist = "artist";
        private const string ExtraSeekMs = "seekMs";
        private const string ExtraDurationMs = "durationMs";

        private IExoPlayer? _player;
        private MediaSession? _mediaSession;
        private Timer? _positionTimer;
        private Handler? _mainHandler;
        private long _expectedDurationMs;
        private string _currentTitle = "YTMusic";
        private string _currentArtist = "Playing";
        private bool _isForegroundStarted;
        private long _lastNotificationUpdateAtMs;

        public static event Action<double, double>? PositionChanged;
        public static event Action<bool>? PlayingStateChanged;
        public static event Action? PlaybackEnded;

        public override void OnCreate()
        {
            base.OnCreate();

            _mainHandler = new Handler(Looper.MainLooper!);
            _player = new ExoPlayerBuilder(this).Build();
            _player.AddListener(this);
            EnsureNotificationChannel();

            _mediaSession = new MediaSession.Builder(this, _player).Build();
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            _ = base.OnStartCommand(intent, flags, startId);
            RunOnMainThread(() => HandleAction(intent));
            return StartCommandResult.Sticky;
        }

        public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo)
        {
            return _mediaSession;
        }

        public override void OnDestroy()
        {
            StopPositionTimer();

            if (_player != null)
            {
                _player.RemoveListener(this);
                _player.Release();
                _player.Dispose();
                _player = null;
            }

            _mediaSession?.Release();
            _mediaSession?.Dispose();
            _mediaSession = null;

            if (_isForegroundStarted)
            {
                StopForeground(StopForegroundFlags.Remove);
                _isForegroundStarted = false;
            }

            _mainHandler = null;
            base.OnDestroy();
        }

        public void OnIsPlayingChanged(bool isPlaying)
        {
            if (isPlaying)
            {
                StartPositionTimer();
            }
            else
            {
                StopPositionTimer();
            }

            PlayingStateChanged?.Invoke(isPlaying);
            UpdateForegroundNotification(force: true);
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == PlaybackStateEnded)
            {
                StopPositionTimer();
                PlayingStateChanged?.Invoke(false);
                UpdateForegroundNotification(force: true);
                PlaybackEnded?.Invoke();
            }
        }

        public static Task PlayAsync(Context context, string source, bool isLocalFile, string? title, string? artist, double? durationSeconds = null)
        {
            var intent = CreateIntent(context, ActionPlaySource);
            intent.PutExtra(ExtraSource, source);
            intent.PutExtra(ExtraIsLocalFile, isLocalFile);
            intent.PutExtra(ExtraTitle, title ?? "YTMusic");
            intent.PutExtra(ExtraArtist, artist ?? "Playing");
            if (durationSeconds.HasValue && durationSeconds.Value > 0)
            {
                intent.PutExtra(ExtraDurationMs, (long)(durationSeconds.Value * 1000));
            }

            StartServiceIntent(context, intent);
            return Task.CompletedTask;
        }

        public static Task PauseAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionPause));
            return Task.CompletedTask;
        }

        public static Task ResumeAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionResume));
            return Task.CompletedTask;
        }

        public static Task SeekAsync(Context context, long positionMs)
        {
            var intent = CreateIntent(context, ActionSeek);
            intent.PutExtra(ExtraSeekMs, Math.Max(0, positionMs));
            StartServiceIntent(context, intent);
            return Task.CompletedTask;
        }

        public static Task StopAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionStop));
            return Task.CompletedTask;
        }

        private void HandleAction(Intent? intent)
        {
            var player = _player;
            if (player == null || intent?.Action == null)
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
                    _currentTitle = intent.GetStringExtra(ExtraTitle) ?? "YTMusic";
                    _currentArtist = intent.GetStringExtra(ExtraArtist) ?? "Playing";
                    _expectedDurationMs = Math.Max(0, intent.GetLongExtra(ExtraDurationMs, 0));

                    var mediaMetadata = new MediaMetadata.Builder()
                        .SetTitle(_currentTitle)
                        .SetArtist(_currentArtist)
                        .Build();

                    var mediaItem = new MediaItem.Builder()
                        .SetMediaMetadata(mediaMetadata)
                        .SetUri(ResolveUri(source, isLocalFile))
                        .Build();

                    player.SetMediaItem(mediaItem);
                    player.Prepare();
                    player.Play();
                    UpdateForegroundNotification(force: true);
                    StartPositionTimer();
                    break;
                }
                case ActionPause:
                    player.Pause();
                    UpdateForegroundNotification(force: true);
                    break;
                case ActionResume:
                    player.Play();
                    UpdateForegroundNotification(force: true);
                    break;
                case ActionSeek:
                {
                    var seekMs = intent.GetLongExtra(ExtraSeekMs, -1);
                    if (seekMs >= 0)
                    {
                        player.SeekTo(seekMs);
                        UpdateForegroundNotification(force: true);
                    }
                    break;
                }
                case ActionStop:
                    player.Stop();
                    StopPositionTimer();
                    _expectedDurationMs = 0;
                    if (_isForegroundStarted)
                    {
                        StopForeground(StopForegroundFlags.Remove);
                        _isForegroundStarted = false;
                    }
                    StopSelf();
                    break;
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

        private void StartPositionTimer()
        {
            StopPositionTimer();
            _positionTimer = new Timer(_ =>
            {
                RunOnMainThread(() =>
                {
                    var player = _player;
                    if (player == null || !player.IsPlaying)
                    {
                        return;
                    }

                    var currentSeconds = Math.Max(0, player.CurrentPosition) / 1000.0;
                    var durationMs = Math.Max(0, player.Duration);
                    if (durationMs <= 0 && _expectedDurationMs > 0)
                    {
                        durationMs = _expectedDurationMs;
                    }

                    var durationSeconds = durationMs / 1000.0;
                    PositionChanged?.Invoke(currentSeconds, durationSeconds);
                    UpdateForegroundNotification(force: false);
                });
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        private void StopPositionTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        private static Intent CreateIntent(Context context, string action)
        {
            var intent = new Intent(context, typeof(PlaybackForegroundService));
            intent.SetAction(action);
            return intent;
        }

        private static void StartServiceIntent(Context context, Intent intent)
        {
            context.StartService(intent);
        }

        private void RunOnMainThread(Action action)
        {
            if (Looper.MyLooper() == Looper.MainLooper)
            {
                action();
                return;
            }

            _mainHandler?.Post(action);
        }

        private void EnsureNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                return;
            }

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager == null || manager.GetNotificationChannel(NotificationChannelId) != null)
            {
                return;
            }

            var channel = new NotificationChannel(NotificationChannelId, "Playback", NotificationImportance.Default)
            {
                Description = "YTMusic playback controls"
            };
            channel.LockscreenVisibility = NotificationVisibility.Public;
            manager.CreateNotificationChannel(channel);
        }

        private void UpdateForegroundNotification(bool force)
        {
            var player = _player;
            if (player == null)
            {
                return;
            }

            var now = SystemClock.ElapsedRealtime();
            if (!force && now - _lastNotificationUpdateAtMs < 1000)
            {
                return;
            }
            _lastNotificationUpdateAtMs = now;

            var isPlaying = player.IsPlaying;
            var positionMs = Math.Max(0, player.CurrentPosition);
            var durationMs = Math.Max(0, player.Duration);
            if (durationMs <= 0 && _expectedDurationMs > 0)
            {
                durationMs = _expectedDurationMs;
            }

            var notification = BuildPlaybackNotification(isPlaying, positionMs, durationMs);
            if (!_isForegroundStarted)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeMediaPlayback);
                }
                else
                {
                    StartForeground(NotificationId, notification);
                }

                _isForegroundStarted = true;
                return;
            }

            NotificationManagerCompat.From(this).Notify(NotificationId, notification);
        }

        private Notification BuildPlaybackNotification(bool isPlaying, long positionMs, long durationMs)
        {
            var openAppIntent = new Intent(this, typeof(global::YTMusic.MainActivity));
            openAppIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var contentPendingIntent = PendingIntent.GetActivity(
                this,
                3001,
                openAppIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var pauseOrResumeAction = isPlaying ? ActionPause : ActionResume;
            var pauseOrResumeIcon = isPlaying
                ? global::Android.Resource.Drawable.IcMediaPause
                : global::Android.Resource.Drawable.IcMediaPlay;
            var pauseOrResumeTitle = isPlaying ? "Pause" : "Play";
            var pauseOrResumeIntent = PendingIntent.GetService(
                this,
                3002,
                CreateIntent(this, pauseOrResumeAction),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var stopIntent = PendingIntent.GetService(
                this,
                3003,
                CreateIntent(this, ActionStop),
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(this, NotificationChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetContentTitle(_currentTitle)
                .SetContentText(_currentArtist)
                .SetContentIntent(contentPendingIntent)
                .SetVisibility((int)NotificationVisibility.Public)
                .SetCategory(NotificationCompat.CategoryTransport)
                .SetOnlyAlertOnce(true)
                .SetOngoing(isPlaying)
                .SetPriority((int)NotificationPriority.High)
                .AddAction(pauseOrResumeIcon, pauseOrResumeTitle, pauseOrResumeIntent)
                .AddAction(global::Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", stopIntent);

            if (durationMs > 0)
            {
                builder.SetProgress((int)Math.Min(int.MaxValue, durationMs), (int)Math.Min(int.MaxValue, positionMs), false);
            }
            else
            {
                builder.SetProgress(0, 0, true);
            }

            return builder.Build();
        }
    }
}
