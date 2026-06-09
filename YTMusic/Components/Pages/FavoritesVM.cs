using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using YTMusic.Components.Dialogs;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
    public enum FavoriteTrackSortMode
    {
        Time = 0,
        Name = 1,
        Downloaded = 2,
        NotDownloaded = 3
    }

    public class FavoritesVM
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;
        private readonly IDialogService _dialogService;
        private List<FavoriteTrack> _rawTracks = new();

        public Action? StateHasChanged { get; set; }

        public List<FavoriteFolder> Folders { get; private set; } = new();
        public List<FavoriteTrack> Tracks { get; private set; } = new();
        public Dictionary<int, int> FolderTrackCounts { get; private set; } = new();
        public Dictionary<int, string?> FolderCoverUrls { get; private set; } = new();
        public Dictionary<int, string?> FolderFirstTrackAuthors { get; private set; } = new();

        public int SelectedFolderId { get; set; } = FavoriteFolderIds.Default;
        public FavoriteTrackSortMode SortMode { get; set; } = FavoriteTrackSortMode.Time;
        public bool IsFolderListView { get; private set; } = true;
        public bool IsLoading { get; private set; } = false;

        public bool IsDownloadedCatalogSelected => FavoriteFolderIds.IsDownloadedCatalog(SelectedFolderId);

        public FavoritesVM(IFavoriteService favoriteService, ILocalMusicService localMusicService, ISnackbar snackbar, IDialogService dialogService)
        {
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
            _snackbar = snackbar;
            _dialogService = dialogService;
        }

        public async Task InitializeAsync()
        {
            IsFolderListView = true;
            await LoadFoldersAsync();
        }

        public async Task LoadFoldersAsync()
        {
            Folders = await _favoriteService.GetFoldersAsync();
            FolderTrackCounts.Clear();
            FolderCoverUrls.Clear();
            FolderFirstTrackAuthors.Clear();

            foreach (var folder in Folders)
            {
                if (FavoriteFolderIds.IsDownloadedCatalog(folder.Id))
                {
                    var downloads = await _localMusicService.GetDownloadedTracksAsync();
                    var ordered = downloads
                        .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
                        .OrderByDescending(track => track.DownloadedDate)
                        .ToList();
                    var first = ordered.FirstOrDefault();
                    FolderTrackCounts[folder.Id] = ordered.Count;
                    FolderCoverUrls[folder.Id] = first?.ThumbnailUrl;
                    FolderFirstTrackAuthors[folder.Id] = first?.Author;
                }
                else
                {
                    var tracks = await _favoriteService.GetTracksAsync(folder.Id, null);
                    var first = tracks.OrderByDescending(track => track.AddedDate).FirstOrDefault();
                    FolderTrackCounts[folder.Id] = tracks.Count;
                    FolderCoverUrls[folder.Id] = first?.ThumbnailUrl;
                    FolderFirstTrackAuthors[folder.Id] = first?.Author;
                }
            }

            StateHasChanged?.Invoke();
        }

        public async Task OpenFolderAsync(int folderId)
        {
            IsFolderListView = false;
            SelectedFolderId = folderId;
            SortMode = FavoriteTrackSortMode.Time;
            await LoadTracksAsync();
        }

        public void BackToFolderList()
        {
            IsFolderListView = true;
            _rawTracks.Clear();
            Tracks.Clear();
            StateHasChanged?.Invoke();
        }

        public string? GetSelectedFolderName()
        {
            return Folders.FirstOrDefault(f => f.Id == SelectedFolderId)?.Name;
        }

        public async Task LoadTracksAsync()
        {
            IsLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                if (IsDownloadedCatalogSelected)
                {
                    _rawTracks = await LoadDownloadedCatalogTracksAsync();
                }
                else
                {
                    _rawTracks = await _favoriteService.GetTracksAsync(SelectedFolderId, null);

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

        public async Task<List<FavoriteTrack>> LoadTracksForFolderAsync(int folderId)
        {
            if (FavoriteFolderIds.IsDownloadedCatalog(folderId))
            {
                return await LoadDownloadedCatalogTracksAsync();
            }

            var tracks = await _favoriteService.GetTracksAsync(folderId, null);
            foreach (var track in tracks)
            {
                var downloadedTrack = await _localMusicService.GetDownloadedTrackByVideoIdAsync(track.VideoId);
                if (downloadedTrack?.IsVideo == true)
                {
                    track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
                }
            }

            return tracks.OrderByDescending(track => track.AddedDate).ToList();
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

        public async Task ConfirmDeleteFolderAsync(FavoriteFolder folder)
        {
            if (FavoriteFolderIds.IsProtectedFolder(folder.Id))
            {
                return;
            }

            var parameters = new DialogParameters<ConfirmDeleteFolderDialog>
            {
                { x => x.FolderName, folder.Name }
            };
            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.ExtraSmall,
                FullWidth = true
            };

            var dialog = await _dialogService.ShowAsync<ConfirmDeleteFolderDialog>("删除收藏夹", parameters, options);
            var result = await dialog.Result;
            if (result == null || result.Canceled)
            {
                return;
            }

            try
            {
                await _favoriteService.DeleteFolderAsync(folder.Id);
                _snackbar.Add($"已删除收藏夹「{folder.Name}」", Severity.Success);
                await LoadFoldersAsync();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"删除收藏夹失败: {ex.Message}", Severity.Error);
            }
        }

        public async Task ConfirmRemoveFavoriteAsync(FavoriteTrack track)
        {
            if (FavoriteFolderIds.IsDownloadedCatalog(track.FolderId))
            {
                return;
            }

            var parameters = new DialogParameters<ConfirmRemoveFavoriteDialog>
            {
                { x => x.TrackTitle, track.Title },
                { x => x.IsDownloaded, track.IsDownloaded }
            };

            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.ExtraSmall,
                FullWidth = true
            };

            var dialog = await _dialogService.ShowAsync<ConfirmRemoveFavoriteDialog>("取消收藏", parameters, options);
            var result = await dialog.Result;
            if (result == null || result.Canceled || result.Data is not string choice)
            {
                return;
            }

            await RemoveFavoriteAsync(track, deleteLocalFile: choice == "delete");
        }

        private async Task RemoveFavoriteAsync(FavoriteTrack track, bool deleteLocalFile)
        {
            try
            {
                if (deleteLocalFile && track.IsDownloaded)
                {
                    var filePath = track.LocalFilePath;
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        await _localMusicService.RemoveDownloadedTrackAsync(track.VideoId, filePath);
                    }

                    if (!string.IsNullOrWhiteSpace(track.LocalVideoFilePath)
                        && !string.Equals(track.LocalVideoFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        CommonTool.FileHelps.FileHelp.DeleteIfExists(track.LocalVideoFilePath);
                    }
                }

                await _favoriteService.RemoveFromFavoritesAsync(track.VideoId, track.FolderId);
                _rawTracks.Remove(track);
                ApplyTrackOrdering();

                if (FolderTrackCounts.TryGetValue(track.FolderId, out var count) && count > 0)
                {
                    FolderTrackCounts[track.FolderId] = count - 1;
                }

                var message = deleteLocalFile && track.IsDownloaded
                    ? $"已取消收藏并删除「{track.Title}」"
                    : $"已取消收藏「{track.Title}」";
                _snackbar.Add(message, Severity.Success);
                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Error removing favorite: {ex.Message}", Severity.Error);
            }
        }
    }
}
