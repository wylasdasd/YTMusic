using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using YTMusic.Components.Dialogs;
using YTMusic.Services;

using YTMusic.Services.Abstractions;

namespace YTMusic.Components.Pages
{
    public enum FavoriteTrackSortMode
    {
        Time = 0,
        Name = 1,
        Downloaded = 2,
        NotDownloaded = 3
    }

    public class FavoritesFolderVM
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;
        private readonly IDialogService _dialogService;
        private List<FavoriteTrack> _rawTracks = new();

        public Action? StateHasChanged { get; set; }

        public int FolderId { get; private set; }
        public string? FolderName { get; private set; }
        public List<FavoriteTrack> Tracks { get; private set; } = new();
        public HashSet<string> FavoritedVideoIds { get; private set; } = new(StringComparer.Ordinal);
        public FavoriteTrackSortMode SortMode { get; set; } = FavoriteTrackSortMode.Time;
        public bool IsLoading { get; private set; } = false;

        public bool IsDownloadedCatalog => FavoriteFolderIds.IsDownloadedCatalog(FolderId);

        public FavoritesFolderVM(IFavoriteService favoriteService, ILocalMusicService localMusicService, ISnackbar snackbar, IDialogService dialogService)
        {
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
            _snackbar = snackbar;
            _dialogService = dialogService;
        }

        public async Task InitializeAsync(int folderId)
        {
            FolderId = folderId;
            SortMode = FavoriteTrackSortMode.Time;

            var folders = await _favoriteService.GetFoldersAsync();
            FolderName = folders.FirstOrDefault(f => f.Id == folderId)?.Name;

            if (FolderName == null && !FavoriteFolderIds.IsDownloadedCatalog(folderId))
            {
                _snackbar.Add("收藏夹不存在。", Severity.Warning);
                return;
            }

            if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
            {
                FolderName = folders.FirstOrDefault(f => f.Id == folderId)?.Name ?? "已下载";
            }

            await LoadTracksAsync();
        }

        public async Task LoadTracksAsync()
        {
            IsLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                if (IsDownloadedCatalog)
                {
                    _rawTracks = await LoadDownloadedCatalogTracksAsync();
                }
                else
                {
                    _rawTracks = await _favoriteService.GetTracksAsync(FolderId, null);

                    foreach (var track in _rawTracks)
                    {
                        var downloadedTrack = await _localMusicService.GetDownloadedTrackByVideoIdAsync(track.VideoId);
                        if (downloadedTrack?.IsVideo == true)
                        {
                            track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
                        }
                    }
                }

                ApplyTrackOrdering();
                await RefreshFavoritedVideoIdsAsync();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to load favorites: {ex.Message}", Severity.Error);
            }
            finally
            {
                IsLoading = false;
                StateHasChanged?.Invoke();
            }
        }

        public bool IsTrackFavoritedAnywhere(FavoriteTrack track)
        {
            if (IsDownloadedCatalog)
            {
                return FavoritedVideoIds.Contains(track.VideoId);
            }

            return track.FolderId == FolderId;
        }

        private async Task RefreshFavoritedVideoIdsAsync()
        {
            FavoritedVideoIds.Clear();
            if (!_rawTracks.Any())
            {
                return;
            }

            var videoIds = _rawTracks
                .Select(track => track.VideoId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToArray();

            if (videoIds.Length == 0)
            {
                return;
            }

            var favorited = await _favoriteService.GetFavoritedVideoIdsAsync(videoIds);
            foreach (var videoId in favorited)
            {
                FavoritedVideoIds.Add(videoId);
            }
        }

        public async Task SetSortModeAsync(FavoriteTrackSortMode sortMode)
        {
            SortMode = sortMode;
            ApplyTrackOrdering();
            StateHasChanged?.Invoke();
        }

        private void ApplyTrackOrdering()
        {
            IEnumerable<FavoriteTrack> query = _rawTracks;

            switch (SortMode)
            {
                case FavoriteTrackSortMode.Downloaded:
                    query = query.Where(track => track.IsDownloaded)
                        .OrderByDescending(track => track.AddedDate);
                    break;
                case FavoriteTrackSortMode.NotDownloaded:
                    query = query.Where(track => !track.IsDownloaded)
                        .OrderByDescending(track => track.AddedDate);
                    break;
                case FavoriteTrackSortMode.Name:
                    query = query.OrderBy(track => track.Title, StringComparer.CurrentCultureIgnoreCase);
                    break;
                default:
                    query = query.OrderByDescending(track => track.AddedDate);
                    break;
            }

            Tracks = query.ToList();
        }

        private async Task<List<FavoriteTrack>> LoadDownloadedCatalogTracksAsync()
        {
            var downloads = await _localMusicService.GetDownloadedTracksAsync();
            return downloads
                .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
                .OrderByDescending(track => track.DownloadedDate)
                .Select(MapDownloadedTrackToFavorite)
                .ToList();
        }

        private static FavoriteTrack MapDownloadedTrackToFavorite(DownloadedTrack track)
        {
            return new FavoriteTrack
            {
                VideoId = track.VideoId,
                FolderId = FavoriteFolderIds.DownloadedCatalog,
                Title = track.Title,
                Author = track.Author,
                ThumbnailUrl = track.ThumbnailUrl,
                AddedDate = track.DownloadedDate,
                LocalFilePath = track.LocalFilePath,
                LocalVideoFilePath = track.IsVideo ? track.LocalFilePath : null
            };
        }

        public async Task DeleteLocalFileAsync(FavoriteTrack track)
        {
            if (!IsDownloadedCatalog || string.IsNullOrWhiteSpace(track.LocalFilePath))
            {
                return;
            }

            try
            {
                await _favoriteService.RemoveTrackByFilePathAsync(track.LocalFilePath);
                await _localMusicService.RemoveDownloadedTrackAsync(track.VideoId, track.LocalFilePath);
                _rawTracks.Remove(track);
                ApplyTrackOrdering();
                _snackbar.Add($"已删除「{track.Title}」", Severity.Success);
                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"删除本地文件失败: {ex.Message}", Severity.Error);
            }
        }

        public async Task OpenFavoriteFolderDialogAsync(FavoriteTrack track)
        {
            var parameters = new DialogParameters<FavoriteFolderDialog>
            {
                { x => x.VideoId, track.VideoId },
                { x => x.Title, track.Title },
                { x => x.Author, track.Author },
                { x => x.ThumbnailUrl, track.ThumbnailUrl },
                { x => x.LocalFilePath, track.LocalFilePath }
            };

            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.Small,
                FullWidth = true
            };

            var dialog = await _dialogService.ShowAsync<FavoriteFolderDialog>("管理收藏夹", parameters, options);
            await dialog.Result;

            if (IsDownloadedCatalog)
            {
                await RefreshFavoritedVideoIdsAsync();
            }
            else
            {
                var stillInCurrentFolder = await _favoriteService.IsFavoriteAsync(track.VideoId, FolderId);
                if (!stillInCurrentFolder)
                {
                    _rawTracks.Remove(track);
                    ApplyTrackOrdering();
                }
            }

            StateHasChanged?.Invoke();
        }
    }
}
