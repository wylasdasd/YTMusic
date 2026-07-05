namespace YTMusic.BLL.Ports;

public interface IDownloadMusicDirectoryProvider
{
    string GetDownloadedMusicDirectory();

    string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames);
}
