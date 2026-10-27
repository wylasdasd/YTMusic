using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions.Data;

public interface IPlaybackHistoryRepository
{
    Task<IReadOnlyList<PlaybackHistoryRecord>> GetAllAsync();

    Task RecordPlayAsync(PlaybackHistoryRecord record);

    Task RemoveForTrackAsync(string? videoId, string? localFilePath);

    Task RemoveByIdAsync(long id);

    Task ClearAsync();
}
