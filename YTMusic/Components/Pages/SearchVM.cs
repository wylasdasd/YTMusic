using YoutubeExplode.Search;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed class SearchVM : ViewModelBase, IDisposable
{
    private readonly IYouTubeService _youTubeService;
    private readonly IUiNotifier _notifier;
    private readonly IDownloadManagerService _downloadManager;
    private readonly IFavoriteService _favoriteService;
    private readonly IDialogHost _dialogHost;
    private readonly INetworkErrorService _networkErrorService;

    public string Query { get; set; } = string.Empty;
    public bool IsSearching { get; private set; }

    public List<VideoSearchResult> Videos { get; } = new();
    public HashSet<string> FavoriteVideoIds { get; } = new();

    private IAsyncEnumerator<VideoSearchResult>? _searchEnumerator;
    public bool HasMore { get; private set; }

    public SearchVM(
        IYouTubeService youTubeService,
        IUiNotifier notifier,
        IDownloadManagerService downloadManager,
        IFavoriteService favoriteService,
        IDialogHost dialogHost,
        INetworkErrorService networkErrorService)
    {
        _youTubeService = youTubeService;
        _notifier = notifier;
        _downloadManager = downloadManager;
        _favoriteService = favoriteService;
        _dialogHost = dialogHost;
        _networkErrorService = networkErrorService;
    }

    public async Task SearchAsync()
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

    public async Task LoadNextPageAsync()
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
