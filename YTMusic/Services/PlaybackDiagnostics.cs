using System;

namespace YTMusic.Services
{
    internal static class PlaybackDiagnostics
    {
        private const string Tag = "YTMusic";

        public static void Log(string message)
        {
            var line = $"[YTMusic:Playback] {message}";
            Console.WriteLine(line);
#if ANDROID
            global::Android.Util.Log.Info(Tag, line);
#endif
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var line = ex == null
                ? $"[YTMusic:Playback] {message}"
                : $"[YTMusic:Playback] {message} | {ex.GetType().Name}: {ex.Message}";
            Console.WriteLine(line);
#if ANDROID
            global::Android.Util.Log.Error(Tag, line);
#endif
            if (ex != null)
            {
                Console.WriteLine(ex.StackTrace);
#if ANDROID
                global::Android.Util.Log.Error(Tag, ex.StackTrace ?? string.Empty);
#endif
            }
        }

        public static string DescribeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "(null)";
            }

            if (url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            {
                return $"proxy:{url}";
            }

            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath} (len={url.Length})";
            }
            catch
            {
                return $"(invalid,len={url.Length})";
            }
        }
    }
}
