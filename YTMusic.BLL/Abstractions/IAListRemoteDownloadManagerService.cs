using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IAListRemoteDownloadManagerService
{
    IReadOnlyList<DownloadTaskInfo> ActiveRemoteDownloads { get; }
    event Action? OnRemoteDownloadsChanged;
    void StartRemoteDownload(AListDirectoryItem item);
    void StartRemoteDirectoryDownload(AListDirectoryItem item, string? displayTitle = null);
}
