using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using YTMusic.Services;
using YTMusic.Components.Dialogs;

namespace YTMusic.Components.Pages
{
    public class DownloadsVM
    {
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;
        private readonly IFavoriteService _favoriteService;
        private readonly IDialogService _dialogService;

        public Action? StateHasChanged { get; set; }

        public List<DownloadedTrack> Files { get; private set; } = new();
        public HashSet<string> FavoriteFilePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsLoading { get; private set; } = true;

        public DownloadsVM(ILocalMusicService localMusicService, ISnackbar snackbar, IFavoriteService favoriteService, IDialogService dialogService)
        {
            _localMusicService = localMusicService;
            _snackbar = snackbar;
            _favoriteService = favoriteService;
            _dialogService = dialogService;
        }

        public async Task LoadFilesAsync()
        {
            IsLoading = true;
            StateHasChanged?.Invoke();

            try
            {
                var files = await _localMusicService.GetDownloadedTracksAsync();
                Files = new List<DownloadedTrack>(files);
                FavoriteFilePaths.Clear();

                // Check which local files are already favorited
                foreach (var file in Files)
                {
                    var track = await _favoriteService.GetTrackByFilePathAsync(file.LocalFilePath);
                    if (track != null)
                    {
                        FavoriteFilePaths.Add(file.LocalFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to load files: {ex.Message}", Severity.Error);
            }
            finally
            {
                IsLoading = false;
                StateHasChanged?.Invoke();
            }
        }

        public async Task DeleteFileAsync(DownloadedTrack file)
        {
            try
            {
                // Delete from DB if it was favorited
                if (FavoriteFilePaths.Contains(file.LocalFilePath))
                {
                    await _favoriteService.RemoveTrackByFilePathAsync(file.LocalFilePath);
                    FavoriteFilePaths.Remove(file.LocalFilePath);
                }

                await _localMusicService.RemoveDownloadedTrackAsync(file.VideoId, file.LocalFilePath);
                Files.Remove(file);
                
                _snackbar.Add($"{file.Title} deleted", Severity.Success);
                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to delete: {ex.Message}", Severity.Error);
            }
        }

        public async Task OpenFavoriteDialogAsync(DownloadedTrack file)
        {
            try
            {
                var parameters = new DialogParameters
                {
                    { "VideoId", file.VideoId },
                    { "Title", file.Title },
                    { "Author", file.Author },
                    { "LocalFilePath", file.LocalFilePath }
                };

                var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
                var dialog = await _dialogService.ShowAsync<YTMusic.Components.Dialogs.FavoriteFolderDialog>("Add to Favorite Folder", parameters, options);
                await dialog.Result;

                // Refresh favorite status after dialog closed
                var stillFavorited = await _favoriteService.GetTrackByFilePathAsync(file.LocalFilePath);
                if (stillFavorited != null)
                {
                    FavoriteFilePaths.Add(file.LocalFilePath);
                }
                else
                {
                    FavoriteFilePaths.Remove(file.LocalFilePath);
                }

                StateHasChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to open favorites: {ex.Message}", Severity.Error);
            }
        }
    }
}