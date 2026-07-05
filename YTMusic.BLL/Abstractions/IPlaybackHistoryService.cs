using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IPlaybackHistoryService
{
    Task<IReadOnlyList<PlaybackHistoryRecord>> GetHistoryAsync();

    Task RecordPlayAsync(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl,
        string? localFilePath,
        bool isVideo,
        double? durationSeconds);

    Task ClearAsync();
}
