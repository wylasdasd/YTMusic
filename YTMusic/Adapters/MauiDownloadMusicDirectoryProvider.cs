using YTMusic.BLL.Ports;
using YTMusic.Infrastructure.Storage;

namespace YTMusic.Adapters;

public sealed class MauiDownloadMusicDirectoryProvider : IDownloadMusicDirectoryProvider
{
    public string GetDownloadedMusicDirectory() => StoragePaths.GetDownloadedMusicDirectory();

    public string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames)
        => StoragePaths.ResolveLocalDownloadDirectory(folderNames);
}
