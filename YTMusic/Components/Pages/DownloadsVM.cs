using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.Services;
using YTMusic.Services.Playback;
using YTMusic.ViewModels.Shared;

namespace YTMusic.Components.Pages;

public sealed partial class DownloadsVM : ViewModelBase
{
    private readonly ILocalMusicService _localMusicService;
    private readonly IDownloadMusicDirectoryProvider _downloadDirectoryProvider;
    private readonly IUiNotifier _notifier;
    private readonly IFavoriteService _favoriteService;
    private readonly IDialogHost _dialogHost;
    private readonly MusicPlayerService _playerService;
    private readonly NavigationManager _navigation;

    public List<DownloadedTrack> Files { get; private set; } = new();
    public IReadOnlyList<LocalDirectoryNode<DownloadedTrack>> DirectoryNodes { get; private set; } = Array.Empty<LocalDirectoryNode<DownloadedTrack>>();
    public HashSet<string> FavoriteFilePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    private List<DownloadedTrack> _downloadedFiles = new();
    private readonly List<ImportedLocalGroup> _importedGroups = new();

    [ObservableProperty]
    private bool _isLoading = true;

    public DownloadsVM(
        ILocalMusicService localMusicService,
        IDownloadMusicDirectoryProvider downloadDirectoryProvider,
        IUiNotifier notifier,
        IFavoriteService favoriteService,
        IDialogHost dialogHost,
        MusicPlayerService playerService,
        NavigationManager navigation)
    {
        _localMusicService = localMusicService;
        _downloadDirectoryProvider = downloadDirectoryProvider;
        _notifier = notifier;
        _favoriteService = favoriteService;
        _dialogHost = dialogHost;
        _playerService = playerService;
        _navigation = navigation;
    }

    public async Task LoadFilesAsync()
    {
        IsLoading = true;
        NotifyChanged();

        try
        {
            var downloads = await _localMusicService.GetDownloadedTracksAsync();
            _downloadedFiles = downloads
                .Where(file => !string.IsNullOrWhiteSpace(file.LocalFilePath))
                .ToList();
            await LoadImportedGroupsAsync();
            RebuildDirectories();
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
            await _favoriteService.RemoveTrackByFilePathAsync(file.LocalFilePath);
            FavoriteFilePaths.Remove(file.LocalFilePath);
            await _localMusicService.RemoveDownloadedTrackAsync(file.VideoId, file.LocalFilePath);
            RemoveListedFile(file);
            _notifier.Success($"已删除「{file.Title}」");
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

    public async Task PlayDirectoryAsync(LocalDirectoryNode<DownloadedTrack> node)
    {
        var tracks = LocalDirectoryTree.Flatten(new[] { node });
        if (tracks.Count == 0)
        {
            return;
        }

        var playlist = PlayingItemMapper.FromDownloadedTracks(tracks, isVideo: false);

        if (await _playerService.PlayPlaylistAsync(playlist, 0, node.Name, PlaybackMode.Sequential))
        {
            _navigation.NavigateTo("/player");
        }
    }

    private async Task LoadImportedGroupsAsync()
    {
        _importedGroups.Clear();
        var downloadedPaths = _downloadedFiles
            .Select(file => file.LocalFilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folders = await _favoriteService.GetFoldersAsync();
        foreach (var folder in folders.Where(item => item.IsLocalImport))
        {
            var tracks = await _favoriteService.GetTracksAsync(folder.Id, null);
            var files = tracks
                .Where(track => !string.IsNullOrWhiteSpace(track.LocalFilePath))
                .Where(track => !downloadedPaths.Contains(track.LocalFilePath!))
                .Select(track => new DownloadedTrack
                {
                    VideoId = track.VideoId,
                    Title = track.Title,
                    Author = track.Author,
                    ThumbnailUrl = track.ThumbnailUrl,
                    LocalFilePath = track.LocalFilePath!,
                    IsVideo = false,
                    DownloadedDate = track.AddedDate
                })
                .ToList();
            if (files.Count == 0)
            {
                continue;
            }

            _importedGroups.Add(new ImportedLocalGroup(folder.LocalRootPath, files));
        }
    }

    private void RemoveListedFile(DownloadedTrack file)
    {
        _downloadedFiles.RemoveAll(item => SameFile(item, file));
        foreach (var group in _importedGroups)
        {
            group.Files.RemoveAll(item => SameFile(item, file));
        }

        RebuildDirectories();
    }

    private static bool SameFile(DownloadedTrack left, DownloadedTrack right)
        => string.Equals(left.LocalFilePath, right.LocalFilePath, StringComparison.OrdinalIgnoreCase)
           && string.Equals(left.VideoId, right.VideoId, StringComparison.Ordinal);

    private void RebuildDirectories()
    {
        var nodes = new List<LocalDirectoryNode<DownloadedTrack>>();
        nodes.AddRange(BuildNodes(_downloadedFiles, _downloadDirectoryProvider.GetDownloadedMusicDirectory()));
        foreach (var group in _importedGroups)
        {
            nodes.AddRange(BuildNodes(group.Files, group.RootPath));
        }

        DirectoryNodes = nodes;
        Files = _downloadedFiles.Concat(_importedGroups.SelectMany(group => group.Files)).ToList();
    }

    private static IReadOnlyList<LocalDirectoryNode<DownloadedTrack>> BuildNodes(
        IReadOnlyList<DownloadedTrack> files,
        string? libraryRoot)
        => LocalDirectoryTree.Build(
            files,
            file => file.LocalFilePath,
            file => file.Title,
            file => file.DownloadedDate,
            libraryRoot,
            sortByName: false);

    private sealed record ImportedLocalGroup(string? RootPath, List<DownloadedTrack> Files);

    public async Task PlayLocalAudioFileAsync(DownloadedTrack file)
    {
        if (await _playerService.PlayLocalFileAsync(file.LocalFilePath, file.Title, false, file.Author, file.ThumbnailUrl, file.VideoId))
        {
            _navigation.NavigateTo("/player");
        }
    }

    public async Task PlayLocalVideoFileAsync(DownloadedTrack file)
    {
        if (await _playerService.PlayLocalFileAsync(file.LocalFilePath, file.Title, true, file.Author, file.ThumbnailUrl, file.VideoId))
        {
            _navigation.NavigateTo("/player");
        }
    }
}
