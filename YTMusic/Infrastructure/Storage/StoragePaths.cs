using CommonTool.FileHelps;

namespace YTMusic.Infrastructure.Storage;

/// <summary>
/// 本地下载目录路径解析（含 Android 外部 Music 目录等平台差异）。
/// </summary>
public static class StoragePaths
{
    /// <summary>本地下载音乐根目录（平台相关路径 + <see cref="AppGlobal.Storage.DownloadedMusicFolderName"/>）。</summary>
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

        return Path.Combine(baseDirectory, AppGlobal.Storage.DownloadedMusicFolderName);
    }

    /// <summary>在下载根目录下解析子文件夹路径（<paramref name="folderNames"/> 取第一个有效名）。</summary>
    public static string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames)
    {
        var primary = folderNames?
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?
            .Trim();

        var baseDir = GetDownloadedMusicDirectory();
        if (string.IsNullOrWhiteSpace(primary))
        {
            return baseDir;
        }

        return Path.Combine(baseDir, FileHelp.SafeFileName(primary));
    }
}
