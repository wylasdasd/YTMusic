using YTMusic.BLL.Ports;
using YTMusic.Services;

namespace YTMusic.Adapters;

public sealed class MauiDownloadMusicDirectoryProvider : IDownloadMusicDirectoryProvider
{
    public string GetDownloadedMusicDirectory() => StoragePaths.GetDownloadedMusicDirectory();
}
