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

        public IReadOnlyList<DownloadTaskInfo> ActiveDownloads => _downloadManager.ActiveDownloads;

        public TransfersVM(IDownloadManagerService downloadManager)
        {
            _downloadManager = downloadManager;
            _downloadManager.OnDownloadsChanged += HandleDownloadsChanged;
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