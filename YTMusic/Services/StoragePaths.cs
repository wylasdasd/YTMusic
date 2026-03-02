using System;
using System.IO;

namespace YTMusic.Services
{
    public static class StoragePaths
    {
        public static string GetDownloadedMusicDirectory()
        {
#if ANDROID
            try
            {
                var musicDir = Android.App.Application.Context?.GetExternalFilesDir(Android.OS.Environment.DirectoryMusic);
                if (musicDir != null && !string.IsNullOrWhiteSpace(musicDir.AbsolutePath))
                {
                    return musicDir.AbsolutePath;
                }
            }
            catch
            {
                // Fall back to app local data directory.
            }
#endif
            string baseDirectory;
            try
            {
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch
            {
                baseDirectory = Environment.CurrentDirectory;
            }

            return Path.Combine(baseDirectory, "DownloadedMusic");
        }
    }
}
