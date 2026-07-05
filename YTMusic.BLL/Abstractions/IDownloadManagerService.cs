using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IDownloadManagerService
{
    IReadOnlyList<DownloadTaskInfo> ActiveDownloads { get; }
    event Action? OnDownloadsChanged;
    void StartDownload(string videoId, string title, bool isVideo);
}
