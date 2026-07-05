using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using YoutubeExplode.Search;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class SearchVM : ViewModelBase, IDisposable
{
    private readonly IYouTubeService _youTubeService;
    private readonly IUiNotifier _notifier;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IFavoriteService _favoriteService;
    private readonly IDialogHost _dialogHost;
    private readonly INetworkErrorService _networkErrorService;
    private readonly MusicPlayerService _playerService;
    private readonly NavigationManager _navigation;
    private readonly GlobalStateService _globalState;

    public string Query { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public List<VideoSearchResult> Videos { get; } = new();
    public HashSet<string> FavoriteVideoIds { get; } = new();

    private IAsyncEnumerator<VideoSearchResult>? _searchEnumerator;

    [ObservableProperty]
    private bool _hasMore;

    public SearchVM(
        IYouTubeService youTubeService,
        IUiNotifier notifier,
        IDownloadManagerService downloadManager,
        IFavoriteService favoriteService,
        IDialogHost dialogHost,
        INetworkErrorService networkErrorService,
        MusicPlayerService playerService,
        NavigationManager navigation,
        GlobalStateService globalState)
    {
        _youTubeService = youTubeService;
        _notifier = notifier;
        _downloadManager = downloadManager;
        _favoriteService = favoriteService;
        _dialogHost = dialogHost;
        _networkErrorService = networkErrorService;
        _playerService = playerService;
        _navigation = navigation;
        _globalState = globalState;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        IsSearching = true;
        Videos.Clear();
        HasMore = false;
        NotifyChanged();

        if (_searchEnumerator != null)
        {
            await _searchEnumerator.DisposeAsync();
        }

        _searchEnumerator = _youTubeService.SearchVideosAsync(Query).GetAsyncEnumerator();
        await LoadNextPageAsync();
    }

    [RelayCommand]
    private async Task LoadNextPageAsync()
    {
        if (_searchEnumerator == null)
        {
            return;
        }

        IsSearching = true;
        NotifyChanged();

        try
        {
            const int itemsToLoad = 12;
            var loaded = 0;
            var newVideos = new List<VideoSearchResult>();

            while (loaded < itemsToLoad && await _searchEnumerator.MoveNextAsync())
            {
                newVideos.Add(_searchEnumerator.Current);
                loaded++;
            }

            var newVideoIds = newVideos
                .Select(video => video.Id.Value)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToArray();

            var favoritedIds = await _favoriteService.GetFavoritedVideoIdsAsync(newVideoIds);
            foreach (var videoId in favoritedIds)
            {
                FavoriteVideoIds.Add(videoId);
            }

            foreach (var video in newVideos)
            {
                Videos.Add(video);
            }

            HasMore = loaded == itemsToLoad;
        }
        catch (Exception ex)
        {
            _ = _networkErrorService.NotifyFailureAsync("搜索", ex);
        }
        finally
        {
            IsSearching = false;
            NotifyChanged();
        }
    }

    public async Task PlayAndNavigateAsync(VideoSearchResult video)
    {
        try
        {
            _globalState.ShowLoading();
            NotifyChanged();

            if (await _playerService.PlayAsync(video))
            {
                _navigation.NavigateTo("/player");
            }
        }
        finally
        {
            _globalState.HideLoading();
            NotifyChanged();
        }
    }

    public void Download(VideoSearchResult video, bool isVideo)
    {
        _downloadManager.StartDownload(video.Id.Value, video.Title, isVideo);
        _notifier.Info($"Added '{video.Title}' to transfers.");
    }

    public async Task OpenFavoriteDialogAsync(VideoSearchResult video)
    {
        try
        {
            var thumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url;
            await _dialogHost.ShowFavoriteFolderDialogAsync(
                new FavoritePickRequest
                {
                    VideoId = video.Id.Value,
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    ThumbnailUrl = thumbnailUrl
                },
                "Add to Favorite Folder");

            if (await _favoriteService.IsFavoriteInAnyFolderAsync(video.Id.Value))
            {
                FavoriteVideoIds.Add(video.Id.Value);
            }
            else
            {
                FavoriteVideoIds.Remove(video.Id.Value);
            }

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to open favorites: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_searchEnumerator != null)
        {
            _searchEnumerator.DisposeAsync().GetAwaiter().GetResult();
            _searchEnumerator = null;
        }
    }
}
