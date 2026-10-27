using System.Linq;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;

namespace YTMusic.Services;

/// <summary>
/// 监听下载任务完成，供下载页在任意导航时机刷新「下载记录」。
/// </summary>
public sealed class DownloadRecordsSync
{
    private readonly IDownloadManagerService _downloadManager;
    private readonly IAListRemoteDownloadManagerService _remoteDownloads;
    private int _completedSignature;

    public event Action? RecordsShouldRefresh;

    public DownloadRecordsSync(
        IDownloadManagerService downloadManager,
        IAListRemoteDownloadManagerService remoteDownloads)
    {
        _downloadManager = downloadManager;
        _remoteDownloads = remoteDownloads;
        _downloadManager.OnDownloadsChanged += OnDownloadsChanged;
        _remoteDownloads.OnRemoteDownloadsChanged += OnDownloadsChanged;
    }

    private void OnDownloadsChanged()
    {
        var signature = BuildCompletedSignature();
        if (signature == _completedSignature)
        {
            return;
        }

        _completedSignature = signature;
        RecordsShouldRefresh?.Invoke();
    }

    private int BuildCompletedSignature()
    {
        return _downloadManager.ActiveDownloads.Count(task => task.Status == DownloadStatus.Completed)
             + _remoteDownloads.ActiveRemoteDownloads.Count(task => task.Status == DownloadStatus.Completed);
    }
}
