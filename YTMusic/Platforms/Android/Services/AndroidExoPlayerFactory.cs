using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;

namespace YTMusic.Platforms.Android.Services
{
    internal static class AndroidExoPlayerFactory
    {
        public static IExoPlayer CreateMusicPlayer(Context context)
        {
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(C.UsageMedia)
                .SetContentType(C.ContentTypeMusic)
                .Build();

            return new ExoPlayerBuilder(context)
                .SetAudioAttributes(audioAttributes, handleAudioFocus: true)
                .SetHandleAudioBecomingNoisy(true)
                .Build();
        }

        public static IExoPlayer CreateVideoPlayer(Context context)
        {
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(C.UsageMedia)
                .SetContentType(C.ContentTypeMovie)
                .Build();

            return new ExoPlayerBuilder(context)
                .SetAudioAttributes(audioAttributes, handleAudioFocus: true)
                .SetHandleAudioBecomingNoisy(true)
                .Build();
        }
    }
}
