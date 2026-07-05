using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services;

namespace YTMusic.Adapters;

public sealed class MauiDownloadMusicDirectoryProvider : IDownloadMusicDirectoryProvider
{
    public string GetDownloadedMusicDirectory() => StoragePaths.GetDownloadedMusicDirectory();

    public string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames)
        => StoragePaths.ResolveLocalDownloadDirectory(folderNames);
}
