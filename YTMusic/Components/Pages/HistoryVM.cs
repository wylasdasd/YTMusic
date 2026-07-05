using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.Services;
using YTMusic.Services.Playback;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class HistoryVM : ViewModelBase
{
    private readonly IPlaybackHistoryService _historyService;
    private readonly MusicPlayerService _playerService;
    private readonly NavigationManager _navigation;

    public IReadOnlyList<PlaybackHistoryRecord> History { get; private set; } = Array.Empty<PlaybackHistoryRecord>();

    public HistoryVM(
        IPlaybackHistoryService historyService,
        MusicPlayerService playerService,
        NavigationManager navigation)
    {
        _historyService = historyService;
        _playerService = playerService;
        _navigation = navigation;
    }

    public async Task LoadAsync()
    {
        History = await _historyService.GetHistoryAsync();
        NotifyChanged();
    }

    public async Task PlayItemAsync(PlaybackHistoryRecord item)
    {
        if (await _playerService.PlayAsync(new PlayingItem
        {
            VideoId = item.VideoId,
            Title = item.Title,
            Author = item.Author,
            ThumbnailUrl = item.ThumbnailUrl,
            LocalFilePath = item.LocalFilePath,
            IsVideo = item.IsVideo,
            DurationSeconds = item.DurationSeconds
        }))
        {
            _navigation.NavigateTo("/player");
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        await _historyService.ClearAsync();
        History = Array.Empty<PlaybackHistoryRecord>();
        NotifyChanged();
    }
}
