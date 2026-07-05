using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed class DownloadsVM : ViewModelBase
{
    private readonly ILocalMusicService _localMusicService;
    private readonly IUiNotifier _notifier;
    private readonly IFavoriteService _favoriteService;
    private readonly IDialogHost _dialogHost;

    public List<DownloadedTrack> Files { get; private set; } = new();
    public HashSet<string> FavoriteFilePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsLoading { get; private set; } = true;

    public DownloadsVM(
        ILocalMusicService localMusicService,
        IUiNotifier notifier,
        IFavoriteService favoriteService,
        IDialogHost dialogHost)
    {
        _localMusicService = localMusicService;
        _notifier = notifier;
        _favoriteService = favoriteService;
        _dialogHost = dialogHost;
    }

    public async Task LoadFilesAsync()
    {
        IsLoading = true;
        NotifyChanged();

        try
        {
            var files = await _localMusicService.GetDownloadedTracksAsync();
            Files = new List<DownloadedTrack>(files);
            FavoriteFilePaths.Clear();

            var filePaths = Files
                .Select(file => file.LocalFilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var favoritedPaths = await _favoriteService.GetFavoritedFilePathsAsync(filePaths);

            foreach (var path in favoritedPaths)
            {
                FavoriteFilePaths.Add(path);
            }
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to load files: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    public async Task DeleteFileAsync(DownloadedTrack file)
    {
        try
        {
            if (FavoriteFilePaths.Contains(file.LocalFilePath))
            {
                await _favoriteService.RemoveTrackByFilePathAsync(file.LocalFilePath);
                FavoriteFilePaths.Remove(file.LocalFilePath);
            }

            await _localMusicService.RemoveDownloadedTrackAsync(file.VideoId, file.LocalFilePath);
            Files.Remove(file);

            _notifier.Success($"{file.Title} deleted");
            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to delete: {ex.Message}");
        }
    }

    public async Task OpenFavoriteDialogAsync(DownloadedTrack file)
    {
        try
        {
            await _dialogHost.ShowFavoriteFolderDialogAsync(
                new FavoritePickRequest
                {
                    VideoId = file.VideoId,
                    Title = file.Title,
                    Author = file.Author,
                    ThumbnailUrl = file.ThumbnailUrl,
                    LocalFilePath = file.LocalFilePath
                },
                "Add to Favorite Folder");

            var stillFavorited = await _favoriteService.GetTrackByFilePathAsync(file.LocalFilePath);
            if (stillFavorited != null)
            {
                FavoriteFilePaths.Add(file.LocalFilePath);
            }
            else
            {
                FavoriteFilePaths.Remove(file.LocalFilePath);
            }

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _notifier.Error($"Failed to open favorites: {ex.Message}");
        }
    }
}
