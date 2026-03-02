using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class DownloadManagerService : IDownloadManagerService
    {
        private readonly IYouTubeService _youTubeService;
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly List<DownloadTaskInfo> _activeDownloads = new();

        public IReadOnlyList<DownloadTaskInfo> ActiveDownloads => _activeDownloads;
        public event Action? OnDownloadsChanged;

        public DownloadManagerService(IYouTubeService youTubeService, IFavoriteService favoriteService, ILocalMusicService localMusicService)
        {
            _youTubeService = youTubeService;
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
        }

        public void StartDownload(string videoId, string title, bool isVideo)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VideoId = videoId,
                Title = title,
                IsVideo = isVideo,
                Status = DownloadStatus.Pending
            };
            
            _activeDownloads.Add(taskInfo);
            OnDownloadsChanged?.Invoke();

            _ = ExecuteDownloadAsync(taskInfo);
        }

        private async Task ExecuteDownloadAsync(DownloadTaskInfo taskInfo)
        {
            taskInfo.Status = DownloadStatus.Downloading;
            OnDownloadsChanged?.Invoke();

            await Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<double>(p => 
                    {
                        taskInfo.Progress = p;
                        OnDownloadsChanged?.Invoke();
                    });

                    string filePath = await _youTubeService.DownloadAsync(taskInfo.VideoId, taskInfo.Title, taskInfo.IsVideo, progress);

                    // Add to Download History Database
                    await _localMusicService.AddDownloadedTrackAsync(new DownloadedTrack
                    {
                        VideoId = taskInfo.VideoId,
                        Title = taskInfo.Title,
                        Author = "Unknown Artist", // Can be enhanced later to fetch real author
                        ThumbnailUrl = $"https://img.youtube.com/vi/{taskInfo.VideoId}/mqdefault.jpg",
                        LocalFilePath = filePath,
                        DownloadedDate = DateTime.UtcNow
                    });

                    // Update favorite if it's there
                    await _favoriteService.UpdateLocalFilePathAsync(taskInfo.VideoId, filePath);

                    taskInfo.Status = DownloadStatus.Completed;
                    taskInfo.Progress = 1.0;
                }
                catch (Exception ex)
                {
                    taskInfo.Status = DownloadStatus.Failed;
                    taskInfo.ErrorMessage = ex.Message;
                }
                finally
                {
                    OnDownloadsChanged?.Invoke();
                }
            });
        }
    }
}