namespace YTMusic.Services
{
    public interface IUploadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveUploads { get; }
        event System.Action? OnUploadsChanged;
        void StartUpload(string localFilePath, string displayName);
    }
}
