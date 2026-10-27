using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Services;

public sealed class LibraryCatalogService : ILibraryCatalogService
{
    private readonly ILocalMusicService _localMusicService;

    public LibraryCatalogService(ILocalMusicService localMusicService)
    {
        _localMusicService = localMusicService;
    }

    public async Task<IReadOnlyList<LibraryItem>> GetDownloadedItemsAsync()
    {
        var downloads = await _localMusicService.GetDownloadedTracksAsync();
        return downloads
            .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
            .OrderByDescending(track => track.DownloadedDate)
            .Select(track => new LibraryItem
            {
                VideoId = track.VideoId,
                Title = track.Title,
                Author = track.Author,
                ThumbnailUrl = track.ThumbnailUrl,
                LocalFilePath = track.LocalFilePath,
                IsVideo = track.IsVideo,
                AddedDate = track.DownloadedDate
            })
            .ToList();
    }
}
