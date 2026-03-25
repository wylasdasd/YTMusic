using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using YTMusic.Services;

namespace YTMusic.Components.Pages
{
    public class FavoritesVM
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;

        public Action? StateHasChanged { get; set; }

        public List<FavoriteFolder> Folders { get; private set; } = new();
        public List<FavoriteTrack> Tracks { get; private set; } = new();

        public int SelectedFolderId { get; set; } = 1;
        
        // 0 = All, 1 = Downloaded, 2 = Not Downloaded
        public int DownloadFilter { get; set; } = 0; 
        
        public bool IsLoading { get; private set; } = false;

        public FavoritesVM(IFavoriteService favoriteService, ILocalMusicService localMusicService, ISnackbar snackbar)
        {
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
            _snackbar = snackbar;
        }

        public async Task InitializeAsync()
        {
            await LoadFoldersAsync();
            await LoadTracksAsync();
        }

        public async Task LoadFoldersAsync()
        {
            Folders = await _favoriteService.GetFoldersAsync();
            if (Folders.Any() && !Folders.Any(f => f.Id == SelectedFolderId))
            {
                SelectedFolderId = Folders.First().Id;
            }
            StateHasChanged?.Invoke();
        }

        public async Task LoadTracksAsync()
        {
            IsLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                bool? isDownloaded = DownloadFilter switch
                {
                    1 => true,
                    2 => false,
                    _ => null
                };

                Tracks = await _favoriteService.GetTracksAsync(SelectedFolderId, isDownloaded);

                foreach (var track in Tracks)
                {
                    var downloadedTrack = await _localMusicService.GetDownloadedTrackByVideoIdAsync(track.VideoId);
                    if (downloadedTrack?.IsVideo == true)
                    {
                        track.LocalVideoFilePath = downloadedTrack.LocalFilePath;
                    }
                }
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

        public async Task SetFolderAsync(int folderId)
        {
            SelectedFolderId = folderId;
            await LoadTracksAsync();
        }

        public async Task SetFilterAsync(int filter)
        {
            DownloadFilter = filter;
            await LoadTracksAsync();
        }

        public async Task RemoveFavoriteAsync(FavoriteTrack track)
        {
            try
            {
                if (track.IsDownloaded)
                {
                    await _favoriteService.DeleteDownloadedFileAndRecordAsync(track.VideoId, track.FolderId);
                }
                else
                {
                    await _favoriteService.RemoveFromFavoritesAsync(track.VideoId, track.FolderId);
                }
                Tracks.Remove(track);
                _snackbar.Add($"Removed '{track.Title}' from favorites.", Severity.Success);
                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Error removing favorite: {ex.Message}", Severity.Error);
            }
        }
    }
}
