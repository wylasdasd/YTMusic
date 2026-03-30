using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
    public class TransfersVM : IDisposable
    {
        private readonly IDownloadManagerService _downloadManager;
        private readonly IUploadManagerService _uploadManager;
        private readonly IAListRemoteDownloadManagerService _aListRemoteDownloadManager;
        public Action? StateHasChanged { get; set; }

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

        public TransfersVM(IDownloadManagerService downloadManager, IUploadManagerService uploadManager, IAListRemoteDownloadManagerService aListRemoteDownloadManager)
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
            StateHasChanged?.Invoke();
        }

        private void HandleDownloadsChanged()
        {
            StateHasChanged?.Invoke();
        }

        public void Dispose()
        {
            _downloadManager.OnDownloadsChanged -= HandleDownloadsChanged;
            _uploadManager.OnUploadsChanged -= HandleDownloadsChanged;
            _aListRemoteDownloadManager.OnRemoteDownloadsChanged -= HandleDownloadsChanged;
        }
    }
}
