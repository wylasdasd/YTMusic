using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;

namespace YTMusic.ViewModels;

public sealed class HistoryVM : ViewModelBase
{
    private readonly IPlaybackHistoryService _historyService;
    private readonly MusicPlayerService _playerService;

    public IReadOnlyList<PlaybackHistoryRecord> History { get; private set; } = Array.Empty<PlaybackHistoryRecord>();

    public HistoryVM(IPlaybackHistoryService historyService, MusicPlayerService playerService)
    {
        _historyService = historyService;
        _playerService = playerService;
    }

    public async Task LoadAsync()
    {
        History = await _historyService.GetHistoryAsync();
        NotifyChanged();
    }

    public async Task<bool> PlayItemAsync(PlaybackHistoryRecord item)
    {
        return await _playerService.PlayAsync(new PlayingItem
        {
            VideoId = item.VideoId,
            Title = item.Title,
            Author = item.Author,
            ThumbnailUrl = item.ThumbnailUrl,
            LocalFilePath = item.LocalFilePath,
            IsVideo = item.IsVideo,
            DurationSeconds = item.DurationSeconds
        });
    }

    public async Task ClearAsync()
    {
        await _historyService.ClearAsync();
        History = Array.Empty<PlaybackHistoryRecord>();
        NotifyChanged();
    }
}
