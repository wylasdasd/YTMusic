using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class TransfersVM : ViewModelBase, IDisposable
{
    private readonly IDownloadManagerService _downloadManager;
    private readonly IUploadManagerService _uploadManager;
    private readonly IAListRemoteDownloadManagerService _aListRemoteDownloadManager;

    public enum TransferFilter
    {
        All,
        Active,
        Completed,
        Failed
    }

    public IReadOnlyList<DownloadTaskInfo> ActiveTransfers =>
        _downloadManager.ActiveDownloads
            .Concat(_uploadManager.ActiveUploads)
            .Concat(_aListRemoteDownloadManager.ActiveRemoteDownloads)
            .OrderBy(task => task.CreatedAtUtc)
            .ToArray();

    public TransferFilter CurrentFilter { get; private set; } = TransferFilter.All;

    public IEnumerable<DownloadTaskInfo> FilteredTransfers =>
        ActiveTransfers.Where(task => CurrentFilter switch
        {
            TransferFilter.Active => task.Status == DownloadStatus.Pending || task.Status == DownloadStatus.Downloading,
            TransferFilter.Completed => task.Status == DownloadStatus.Completed,
            TransferFilter.Failed => task.Status == DownloadStatus.Failed,
            _ => true
        });

    public TransfersVM(
        IDownloadManagerService downloadManager,
        IUploadManagerService uploadManager,
        IAListRemoteDownloadManagerService aListRemoteDownloadManager)
    {
        _downloadManager = downloadManager;
        _uploadManager = uploadManager;
        _aListRemoteDownloadManager = aListRemoteDownloadManager;
        _downloadManager.OnDownloadsChanged += HandleDownloadsChanged;
        _uploadManager.OnUploadsChanged += HandleDownloadsChanged;
        _aListRemoteDownloadManager.OnRemoteDownloadsChanged += HandleDownloadsChanged;
    }

    public void SetFilter(TransferFilter filter)
    {
        if (CurrentFilter == filter)
        {
            return;
        }

        CurrentFilter = filter;
        NotifyChanged();
    }

    public static MudBlazor.Color GetStatusColor(DownloadStatus status) => status switch
    {
        DownloadStatus.Pending => MudBlazor.Color.Default,
        DownloadStatus.Downloading => MudBlazor.Color.Info,
        DownloadStatus.Completed => MudBlazor.Color.Success,
        DownloadStatus.Failed => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default
    };

    public static string GetEmptyMessage(TransferFilter filter) => filter switch
    {
        TransferFilter.Active => "No active transfers.",
        TransferFilter.Completed => "No completed transfers yet.",
        TransferFilter.Failed => "No failed transfers.",
        _ => "No transfers yet."
    };

    public static string GetTaskLabel(DownloadTaskInfo task)
    {
        if (task.Kind == TransferKind.Upload)
        {
            return $"Upload - {task.Status}";
        }

        if (task.Kind == TransferKind.RemoteDownload)
        {
            return $"AList Download - {task.Status}";
        }

        return $"{(task.IsVideo ? "Video" : "Audio")} Download - {task.Status}";
    }

    private void HandleDownloadsChanged()
    {
        NotifyChanged();
    }

    public void Dispose()
    {
        _downloadManager.OnDownloadsChanged -= HandleDownloadsChanged;
        _uploadManager.OnUploadsChanged -= HandleDownloadsChanged;
        _aListRemoteDownloadManager.OnRemoteDownloadsChanged -= HandleDownloadsChanged;
    }
}
