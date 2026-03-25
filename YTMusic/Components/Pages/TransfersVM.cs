using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
    public class TransfersVM : IDisposable
    {
        private readonly IDownloadManagerService _downloadManager;
        public Action? StateHasChanged { get; set; }

        public enum TransferFilter
        {
            All,
            Active,
            Completed,
            Failed
        }

        public IReadOnlyList<DownloadTaskInfo> ActiveDownloads => _downloadManager.ActiveDownloads;
        public TransferFilter CurrentFilter { get; private set; } = TransferFilter.All;

        public IEnumerable<DownloadTaskInfo> FilteredDownloads =>
            ActiveDownloads.Where(task => CurrentFilter switch
            {
                TransferFilter.Active => task.Status == DownloadStatus.Pending || task.Status == DownloadStatus.Downloading,
                TransferFilter.Completed => task.Status == DownloadStatus.Completed,
                TransferFilter.Failed => task.Status == DownloadStatus.Failed,
                _ => true
            });

        public TransfersVM(IDownloadManagerService downloadManager)
        {
            _downloadManager = downloadManager;
            _downloadManager.OnDownloadsChanged += HandleDownloadsChanged;
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
        }
    }
}
