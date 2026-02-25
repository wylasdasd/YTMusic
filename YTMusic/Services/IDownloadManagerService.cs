namespace YTMusic.Services
{
    public enum DownloadStatus { Pending, Downloading, Completed, Failed }

    public class DownloadTaskInfo
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsVideo { get; set; }
        public double Progress { get; set; }
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public string? ErrorMessage { get; set; }
    }

    public interface IDownloadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveDownloads { get; }
        event System.Action? OnDownloadsChanged;
        void StartDownload(string videoId, string title, bool isVideo);
    }
}