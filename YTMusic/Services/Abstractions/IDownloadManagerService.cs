using YTMusic.Services;

namespace YTMusic.Services.Abstractions
{
    public interface IDownloadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveDownloads { get; }
        event System.Action? OnDownloadsChanged;
        void StartDownload(string videoId, string title, bool isVideo);
    }
}
