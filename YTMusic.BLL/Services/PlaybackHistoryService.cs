using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Services;

public sealed class PlaybackHistoryService : IPlaybackHistoryService
{
    private readonly IPlaybackHistoryRepository _repository;

    public PlaybackHistoryService(IPlaybackHistoryRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PlaybackHistoryRecord>> GetHistoryAsync()
        => _repository.GetAllAsync();

    public Task RecordPlayAsync(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl,
        string? localFilePath,
        bool isVideo,
        double? durationSeconds)
    {
        return _repository.RecordPlayAsync(new PlaybackHistoryRecord
        {
            VideoId = videoId,
            Title = title,
            Author = author,
            ThumbnailUrl = thumbnailUrl,
            LocalFilePath = localFilePath,
            IsVideo = isVideo,
            DurationSeconds = durationSeconds,
            PlayedAtUtc = DateTime.UtcNow
        });
    }

    public Task ClearAsync() => _repository.ClearAsync();
}
