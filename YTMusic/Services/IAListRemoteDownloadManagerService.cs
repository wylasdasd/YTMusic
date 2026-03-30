namespace YTMusic.Services
{
    public interface IAListRemoteDownloadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveRemoteDownloads { get; }
        event System.Action? OnRemoteDownloadsChanged;
        void StartRemoteDownload(AListDirectoryItem item);
    }
}
