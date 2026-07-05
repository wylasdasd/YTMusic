using CommonTool.FileHelps;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services;

public sealed class LocalMusicService : ILocalMusicService
{
    private readonly IFavoriteService _favoriteService;
    private readonly IDownloadedTrackRepository _repository;
    private readonly IDownloadMusicDirectoryProvider _downloadMusicDirectoryProvider;
    private readonly IFileSystem _fileSystem;

    public LocalMusicService(
        IFavoriteService favoriteService,
        IDownloadedTrackRepository repository,
        IDownloadMusicDirectoryProvider downloadMusicDirectoryProvider,
        IFileSystem fileSystem)
    {
        _favoriteService = favoriteService;
        _repository = repository;
        _downloadMusicDirectoryProvider = downloadMusicDirectoryProvider;
        _fileSystem = fileSystem;
    }

    public async Task<IReadOnlyList<DownloadedTrack>> GetDownloadedTracksAsync()
    {
        var tracks = await _repository.GetAllAsync();
        var validTracks = new List<DownloadedTrack>();

        foreach (var track in tracks)
        {
            if (_fileSystem.FileExists(track.LocalFilePath))
            {
                validTracks.Add(track);
            }
            else
            {
                await RemoveDownloadedTrackAsync(track.VideoId, string.Empty);
            }
        }

        return validTracks;
    }

    public async Task<DownloadedTrack?> GetDownloadedTrackByVideoIdAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        var track = await _repository.GetByVideoIdAsync(videoId);
        if (track == null)
        {
            return null;
        }

        if (_fileSystem.FileExists(track.LocalFilePath))
        {
            return track;
        }

        await RemoveDownloadedTrackAsync(videoId, string.Empty);
        return null;
    }

    public async Task<DownloadedTrack?> GetDownloadedTrackByFilePathAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var track = await _repository.GetByFilePathAsync(filePath);
        if (track == null)
        {
            return null;
        }

        if (_fileSystem.FileExists(track.LocalFilePath))
        {
            return track;
        }

        await RemoveDownloadedTrackAsync(track.VideoId, string.Empty);
        return null;
    }

    public async Task<DownloadedTrack?> GetDownloadedTrackByRemoteSourcePathAsync(string remoteSourcePath)
    {
        if (string.IsNullOrWhiteSpace(remoteSourcePath))
        {
            return null;
        }

        var track = await _repository.GetByRemoteSourcePathAsync(remoteSourcePath);
        if (track == null)
        {
            return null;
        }

        if (_fileSystem.FileExists(track.LocalFilePath))
        {
            return track;
        }

        await RemoveDownloadedTrackAsync(track.VideoId, string.Empty);
        return null;
    }

    public Task AddDownloadedTrackAsync(DownloadedTrack track) => _repository.AddOrReplaceAsync(track);

    public Task MarkTrackUploadedAsync(string localFilePath, string remotePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            return Task.CompletedTask;
        }

        return _repository.MarkUploadedAsync(localFilePath, remotePath, DateTime.UtcNow);
    }

    public Task ResetAllAsync() => _repository.ResetAllAsync();

    public async Task RemoveDownloadedTrackAsync(string videoId, string filePath)
    {
        await _repository.DeleteByVideoIdAsync(videoId);
        FileHelp.DeleteIfExists(filePath);
    }

    public Task<IReadOnlyList<LocalAudioFile>> GetDownloadedAudioFilesAsync()
    {
        var directory = _downloadMusicDirectoryProvider.GetDownloadedMusicDirectory();
        if (!_fileSystem.DirectoryExists(directory))
        {
            return Task.FromResult<IReadOnlyList<LocalAudioFile>>(new List<LocalAudioFile>());
        }

        var files = _fileSystem.GetFiles(directory);
        var result = files.Select(file =>
        {
            var fileName = Path.GetFileName(file);
            var title = Path.GetFileNameWithoutExtension(file);
            var extension = Path.GetExtension(file);
            return new LocalAudioFile
            {
                FileName = fileName,
                FilePath = file,
                Title = title,
                Extension = extension
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<LocalAudioFile>>(result);
    }

    public async Task DeleteAudioFileAsync(string filePath)
    {
        FileHelp.DeleteIfExists(filePath);
        await _favoriteService.RemoveTrackByFilePathAsync(filePath);
    }
}
