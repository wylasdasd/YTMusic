using YTMusic.Services;

namespace YTMusic.Services.Abstractions
{
    public interface IUploadManagerService
    {
        System.Collections.Generic.IReadOnlyList<DownloadTaskInfo> ActiveUploads { get; }
        event System.Action? OnUploadsChanged;
        void StartUpload(DownloadedTrack track);
        void StartUpload(string localFilePath, string displayName);
    }
}
