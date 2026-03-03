using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using YoutubeExplode.Search;
using YTMusic.Services;
using YTMusic.Components.Dialogs;

namespace YTMusic.Components.Pages
{
    public partial class Search : ComponentBase, IDisposable
    {
        [Inject] private SearchVM ViewModel { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private YTMusic.Services.MusicPlayerService PlayerService { get; set; } = default!;
        [Inject] private YTMusic.Services.GlobalStateService GlobalState { get; set; } = default!;

        protected override void OnInitialized()
        {
            ViewModel.StateHasChanged = StateHasChanged;
        }

        public async Task PlayAndNavigate(VideoSearchResult video)
        {
            try
            {
                GlobalState.ShowLoading();
                StateHasChanged();

                await PlayerService.PlayAsync(video);
                NavigationManager.NavigateTo("/player");
            }
            finally
            {
                GlobalState.HideLoading();
                StateHasChanged();
            }
        }

        public void Dispose()
        {
            // Do not dispose the ViewModel since it's now a Singleton
        }
    }

    public class SearchVM : IDisposable
    {
        private readonly IYouTubeService _youTubeService;
        private readonly ISnackbar _snackbar;
        private readonly IDownloadManagerService _downloadManager;
        private readonly IFavoriteService _favoriteService;
        private readonly IDialogService _dialogService;

        public Action? StateHasChanged { get; set; }

        public string Query { get; set; } = string.Empty;
        public bool IsSearching { get; private set; } = false;

        public List<VideoSearchResult> Videos { get; } = new();
        public HashSet<string> FavoriteVideoIds { get; } = new();

        private IAsyncEnumerator<VideoSearchResult>? _searchEnumerator;
        public bool HasMore { get; private set; } = false;

        public SearchVM(IYouTubeService youTubeService, ISnackbar snackbar, IDownloadManagerService downloadManager, IFavoriteService favoriteService, IDialogService dialogService)
        {
            _youTubeService = youTubeService;
            _snackbar = snackbar;
            _downloadManager = downloadManager;
            _favoriteService = favoriteService;
            _dialogService = dialogService;
        }

        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(Query)) return;

            IsSearching = true;
            Videos.Clear();
            HasMore = false;
            StateHasChanged?.Invoke();

            if (_searchEnumerator != null)
            {
                await _searchEnumerator.DisposeAsync();
            }
            
            _searchEnumerator = _youTubeService.SearchVideosAsync(Query).GetAsyncEnumerator();

            await LoadNextPageAsync();
        }

        public async Task LoadNextPageAsync()
        {
            if (_searchEnumerator == null) return;

            IsSearching = true;
            StateHasChanged?.Invoke();

            try
            {
                int itemsToLoad = 12; // Load 12 items per page
                int loaded = 0;
                
                var newVideos = new List<VideoSearchResult>();

                while (loaded < itemsToLoad && await _searchEnumerator.MoveNextAsync())
                {
                    newVideos.Add(_searchEnumerator.Current);
                    loaded++;
                }
                
                // Fetch favorite states for these new videos in one batch query
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

                // If we loaded fewer than requested, we might have reached the end
                HasMore = loaded == itemsToLoad;
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Search failed: {ex.Message}", Severity.Error);
                HasMore = false;
            }
            finally
            {
                IsSearching = false;
                StateHasChanged?.Invoke();
            }
        }

        public void Download(VideoSearchResult video, bool isVideo)
        {
            _downloadManager.StartDownload(video.Id.Value, video.Title, isVideo);
            _snackbar.Add($"Added '{video.Title}' to transfers.", Severity.Info);
        }

        public async Task OpenFavoriteDialogAsync(VideoSearchResult video)
        {
            try
            {
                var thumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url;
                var parameters = new DialogParameters<YTMusic.Components.Dialogs.FavoriteFolderDialog>
                {
                    { x => x.VideoId, video.Id.Value },
                    { x => x.Title, video.Title },
                    { x => x.Author, video.Author.ChannelTitle },
                    { x => x.ThumbnailUrl, thumbnailUrl }
                };

                var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
                var dialog = await _dialogService.ShowAsync<YTMusic.Components.Dialogs.FavoriteFolderDialog>("Add to Favorite Folder", parameters, options);
                await dialog.Result;

                // Refresh favorite status after dialog closed
                if (await _favoriteService.IsFavoriteInAnyFolderAsync(video.Id.Value))
                {
                    FavoriteVideoIds.Add(video.Id.Value);
                }
                else
                {
                    FavoriteVideoIds.Remove(video.Id.Value);
                }

                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to open favorites: {ex.Message}", Severity.Error);
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
}
