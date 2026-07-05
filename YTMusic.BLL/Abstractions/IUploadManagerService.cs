using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IUploadManagerService
{
    IReadOnlyList<DownloadTaskInfo> ActiveUploads { get; }
    event Action? OnUploadsChanged;
    void StartUpload(DownloadedTrack track);
    void StartUpload(string localFilePath, string displayName);
}
