using YTMusic.Services;

namespace YTMusic.Services.Abstractions
{
    public interface IAListRemoteDownloadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveRemoteDownloads { get; }
        event System.Action? OnRemoteDownloadsChanged;
        void StartRemoteDownload(AListDirectoryItem item);
        void StartRemoteDirectoryDownload(AListDirectoryItem item, string? displayTitle = null);
    }
}
