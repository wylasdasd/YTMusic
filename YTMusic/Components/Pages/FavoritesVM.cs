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
    public class FavoritesVM
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly ISnackbar _snackbar;
        private readonly IDialogService _dialogService;

        public Action? StateHasChanged { get; set; }

        public List<FavoriteFolder> Folders { get; private set; } = new();
        public Dictionary<int, int> FolderTrackCounts { get; private set; } = new();
        public Dictionary<int, string?> FolderCoverUrls { get; private set; } = new();
        public Dictionary<int, string?> FolderFirstTrackAuthors { get; private set; } = new();

        public FavoritesVM(IFavoriteService favoriteService, ILocalMusicService localMusicService, ISnackbar snackbar, IDialogService dialogService)
        {
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
            _snackbar = snackbar;
            _dialogService = dialogService;
        }

        public async Task InitializeAsync()
        {
            await LoadFoldersAsync();
        }

        public async Task OpenCreateFolderDialogAsync()
        {
            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.Small,
                FullWidth = true
            };

            var dialog = await _dialogService.ShowAsync<CreateFavoriteFolderDialog>("新建收藏夹", options);
            var result = await dialog.Result;
            if (result == null || result.Canceled || result.Data is not string name || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                await _favoriteService.CreateFolderAsync(name.Trim());
                _snackbar.Add($"已创建收藏夹「{name.Trim()}」", Severity.Success);
                await LoadFoldersAsync();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"创建收藏夹失败: {ex.Message}", Severity.Error);
            }
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

        public async Task ConfirmClearLocalDownloadsAsync()
        {
            var downloads = await _localMusicService.GetDownloadedTracksAsync();
            if (!downloads.Any())
            {
                return;
            }

            var parameters = new DialogParameters<ConfirmDeleteLocalFilesDialog>
            {
                { x => x.Count, downloads.Count }
            };
            var options = new DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = MaxWidth.ExtraSmall,
                FullWidth = true
            };

            var dialog = await _dialogService.ShowAsync<ConfirmDeleteLocalFilesDialog>("删除本地文件", parameters, options);
            var result = await dialog.Result;
            if (result == null || result.Canceled)
            {
                return;
            }

            try
            {
                foreach (var file in downloads)
                {
                    if (!string.IsNullOrWhiteSpace(file.LocalFilePath))
                    {
                        await _favoriteService.RemoveTrackByFilePathAsync(file.LocalFilePath);
                    }

                    await _localMusicService.RemoveDownloadedTrackAsync(file.VideoId, file.LocalFilePath);
                }

                _snackbar.Add($"已删除 {downloads.Count} 个本地文件", Severity.Success);
                await LoadFoldersAsync();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"删除本地文件失败: {ex.Message}", Severity.Error);
            }
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
    }
}
