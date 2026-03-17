using Android.App;
using Android.Content;
using Android.OS;
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

        private IExoPlayer? _player;
        private MediaSession? _mediaSession;
        private Timer? _positionTimer;
        private Handler? _mainHandler;

        public static event Action<double, double>? PositionChanged;
        public static event Action<bool>? PlayingStateChanged;
        public static event Action? PlaybackEnded;

        public override void OnCreate()
        {
            base.OnCreate();

            _mainHandler = new Handler(Looper.MainLooper!);
            _player = new ExoPlayerBuilder(this).Build();
            _player.AddListener(this);

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
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == PlaybackStateEnded)
            {
                StopPositionTimer();
                PlayingStateChanged?.Invoke(false);
                PlaybackEnded?.Invoke();
            }
        }

        public static Task PlayAsync(Context context, string source, bool isLocalFile, string? title, string? artist)
        {
            var intent = CreateIntent(context, ActionPlaySource);
            intent.PutExtra(ExtraSource, source);
            intent.PutExtra(ExtraIsLocalFile, isLocalFile);
            intent.PutExtra(ExtraTitle, title ?? "YTMusic");
            intent.PutExtra(ExtraArtist, artist ?? "Playing");
            StartServiceIntent(context, intent, foreground: true);
            return Task.CompletedTask;
        }

        public static Task PauseAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionPause), foreground: false);
            return Task.CompletedTask;
        }

        public static Task ResumeAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionResume), foreground: false);
            return Task.CompletedTask;
        }

        public static Task SeekAsync(Context context, long positionMs)
        {
            var intent = CreateIntent(context, ActionSeek);
            intent.PutExtra(ExtraSeekMs, Math.Max(0, positionMs));
            StartServiceIntent(context, intent, foreground: false);
            return Task.CompletedTask;
        }

        public static Task StopAsync(Context context)
        {
            StartServiceIntent(context, CreateIntent(context, ActionStop), foreground: false);
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
                    var title = intent.GetStringExtra(ExtraTitle) ?? "YTMusic";
                    var artist = intent.GetStringExtra(ExtraArtist) ?? "Playing";

                    var mediaMetadata = new MediaMetadata.Builder()
                        .SetTitle(title)
                        .SetArtist(artist)
                        .Build();

                    var mediaItem = new MediaItem.Builder()
                        .SetMediaMetadata(mediaMetadata)
                        .SetUri(ResolveUri(source, isLocalFile))
                        .Build();

                    player.SetMediaItem(mediaItem);
                    player.Prepare();
                    player.Play();
                    StartPositionTimer();
                    break;
                }
                case ActionPause:
                    player.Pause();
                    break;
                case ActionResume:
                    player.Play();
                    break;
                case ActionSeek:
                {
                    var seekMs = intent.GetLongExtra(ExtraSeekMs, -1);
                    if (seekMs >= 0)
                    {
                        player.SeekTo(seekMs);
                    }
                    break;
                }
                case ActionStop:
                    player.Stop();
                    StopPositionTimer();
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
                    var durationSeconds = Math.Max(0, player.Duration) / 1000.0;
                    PositionChanged?.Invoke(currentSeconds, durationSeconds);
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

        private static void StartServiceIntent(Context context, Intent intent, bool foreground)
        {
            if (foreground && Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
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
    }
}
