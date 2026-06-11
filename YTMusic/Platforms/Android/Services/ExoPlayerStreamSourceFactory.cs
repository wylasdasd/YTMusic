using Android.Content;
using Android.Runtime;
using AndroidUri = Android.Net.Uri;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using YTMusic.Services;

namespace YTMusic.Platforms.Android.Services
{
    internal static class ExoPlayerStreamSourceFactory
    {
        private const string DefaultUserAgent = "Mozilla/5.0 (Linux; Android 10) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

        public static void SetPlayerSource(
            IExoPlayer player,
            Context context,
            string source,
            bool isLocalFile,
            string? companionAudioUrl,
            bool autoPlay = true)
        {
            player.Stop();
            player.ClearMediaItems();
            player.PlayWhenReady = autoPlay;

            if (!string.IsNullOrWhiteSpace(companionAudioUrl))
            {
                var merged = CreateMergingMediaSource(context, source, isLocalFile, companionAudioUrl);
                player.SetMediaSource(merged.JavaCast<IMediaSource>()!);
                PlaybackDiagnostics.Log(
                    $"ExoPlayerStreamSource merged autoPlay={autoPlay} video={PlaybackDiagnostics.DescribeUrl(source)} audio={PlaybackDiagnostics.DescribeUrl(companionAudioUrl)}");
            }
            else
            {
                var mediaItem = new MediaItem.Builder()
                    .SetUri(ResolveUri(source, isLocalFile))
                    .Build();
                player.SetMediaItem(mediaItem);
                PlaybackDiagnostics.Log(
                    $"ExoPlayerStreamSource single autoPlay={autoPlay} url={PlaybackDiagnostics.DescribeUrl(source)} local={isLocalFile}");
            }

            player.Prepare();
            if (autoPlay)
            {
                player.Play();
            }
        }

        private static Java.Lang.Object CreateMergingMediaSource(
            Context context,
            string videoUrl,
            bool videoIsLocalFile,
            string audioUrl)
        {
            var videoSource = CreateProgressiveMediaSource(context, videoUrl, videoIsLocalFile);
            var audioSource = CreateProgressiveMediaSource(context, audioUrl, isLocalFile: false);

            var mediaSourceClass = Java.Lang.Class.ForName("androidx.media3.exoplayer.source.MediaSource")!;
            var mediaSourceArrayClass = Java.Lang.Class.ForName("[Landroidx.media3.exoplayer.source.MediaSource;")!;
            var sources = Java.Lang.Reflect.Array.NewInstance(mediaSourceClass, 2);
            Java.Lang.Reflect.Array.Set(sources, 0, videoSource);
            Java.Lang.Reflect.Array.Set(sources, 1, audioSource);

            var mergingClass = Java.Lang.Class.ForName("androidx.media3.exoplayer.source.MergingMediaSource")!;
            var ctor = mergingClass.GetConstructor(
                Java.Lang.Boolean.Type!,
                Java.Lang.Boolean.Type!,
                mediaSourceArrayClass)!;

            return ctor.NewInstance(
                Java.Lang.Boolean.ValueOf(true),
                Java.Lang.Boolean.ValueOf(true),
                sources)!;
        }

        private static Java.Lang.Object CreateProgressiveMediaSource(Context context, string source, bool isLocalFile)
        {
            var mediaItem = MediaItem.FromUri(ResolveUri(source, isLocalFile));

            var httpFactoryClass = Java.Lang.Class.ForName("androidx.media3.datasource.DefaultHttpDataSource$Factory")!;
            var httpFactory = httpFactoryClass.GetConstructor()!.NewInstance()!;
            httpFactoryClass
                .GetMethod("setUserAgent", Java.Lang.Class.FromType(typeof(Java.Lang.String)))!
                .Invoke(httpFactory, DefaultUserAgent);
            httpFactoryClass
                .GetMethod("setAllowCrossProtocolRedirects", Java.Lang.Boolean.Type!)!
                .Invoke(httpFactory, Java.Lang.Boolean.ValueOf(true));

            var dataSourceFactoryClass = Java.Lang.Class.ForName("androidx.media3.datasource.DataSource$Factory")!;
            var defaultDataSourceFactoryClass = Java.Lang.Class.ForName("androidx.media3.datasource.DefaultDataSource$Factory")!;
            var defaultDataSourceFactory = defaultDataSourceFactoryClass
                .GetConstructor(
                    Java.Lang.Class.FromType(typeof(Context)),
                    dataSourceFactoryClass)!
                .NewInstance(context, httpFactory)!;

            var progressiveFactoryClass = Java.Lang.Class.ForName("androidx.media3.exoplayer.source.ProgressiveMediaSource$Factory")!;
            var progressiveFactory = progressiveFactoryClass
                .GetConstructor(dataSourceFactoryClass)!
                .NewInstance(defaultDataSourceFactory)!;

            var mediaItemClass = Java.Lang.Class.ForName("androidx.media3.common.MediaItem")!;
            return progressiveFactoryClass
                .GetMethod("createMediaSource", mediaItemClass)!
                .Invoke(progressiveFactory, mediaItem)!;
        }

        private static AndroidUri ResolveUri(string source, bool isLocalFile)
        {
            if (!isLocalFile)
            {
                return AndroidUri.Parse(source)!;
            }

            if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return AndroidUri.Parse(source)!;
            }

            var file = new Java.IO.File(source);
            return AndroidUri.FromFile(file)!;
        }
    }
}
