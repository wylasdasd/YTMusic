using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace YTMusic.Platforms.Android.Services
{
    [Service(
        Exported = false,
        ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
    public class PlaybackForegroundService : Service
    {
        private const string ChannelId = "ytmusic_playback_channel";
        private const int NotificationId = 20260302;
        private const string ActionStart = "YTMusic.Playback.Start";
        private const string ActionStop = "YTMusic.Playback.Stop";

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == ActionStop)
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            CreateChannel();
            var title = intent?.GetStringExtra("title") ?? "YTMusic";
            var artist = intent?.GetStringExtra("artist") ?? "Playing";

            var notification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle(title)
                .SetContentText(artist)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetOngoing(true)
                .SetPriority((int)NotificationPriority.Low)
                .Build();

            StartForeground(NotificationId, notification);
            return StartCommandResult.Sticky;
        }

        private void CreateChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                return;
            }

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager == null || manager.GetNotificationChannel(ChannelId) != null)
            {
                return;
            }

            var channel = new NotificationChannel(ChannelId, "Playback", NotificationImportance.Low)
            {
                Description = "YTMusic playback controls"
            };
            manager.CreateNotificationChannel(channel);
        }

        public static void Start(Context context, string? title, string? artist)
        {
            var intent = new Intent(context, typeof(PlaybackForegroundService));
            intent.SetAction(ActionStart);
            intent.PutExtra("title", title ?? "YTMusic");
            intent.PutExtra("artist", artist ?? "Playing");
            context.StartForegroundService(intent);
        }

        public static void Stop(Context context)
        {
            var intent = new Intent(context, typeof(PlaybackForegroundService));
            intent.SetAction(ActionStop);
            context.StartService(intent);
        }
    }
}
