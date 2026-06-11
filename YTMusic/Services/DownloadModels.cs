namespace YTMusic.Services
{
    public enum TransferKind { Download, Upload, RemoteDownload }
    public enum DownloadStatus { Pending, Downloading, Completed, Failed }

    public class DownloadTaskInfo
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public System.DateTime CreatedAtUtc { get; set; } = System.DateTime.UtcNow;
        public string TaskKey { get; set; } = string.Empty;
        public TransferKind Kind { get; set; } = TransferKind.Download;
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsVideo { get; set; }
        public double Progress { get; set; }
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? SourcePath { get; set; }
        public string? DestinationPath { get; set; }
    }
}
