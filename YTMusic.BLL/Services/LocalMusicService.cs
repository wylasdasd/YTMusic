using CommonHelp;
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
            if (HasExistingLocalFile(track.LocalFilePath))
            {
                validTracks.Add(track);
            }
        }

        return validTracks;
    }

    public Task<IReadOnlyList<DownloadedTrack>> GetDownloadRecordsAsync()
        => _repository.GetAllAsync();

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

        return HasExistingLocalFile(track.LocalFilePath) ? track : null;
    }

    public Task<DownloadedTrack?> GetDownloadedTrackRecordAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return Task.FromResult<DownloadedTrack?>(null);
        }

        return _repository.GetByVideoIdAsync(videoId);
    }

    public async Task<IReadOnlyDictionary<string, DownloadedTrack>> GetDownloadedTracksByVideoIdsAsync(IEnumerable<string> videoIds)
    {
        var tracks = await _repository.GetByVideoIdsAsync(videoIds);
        var result = new Dictionary<string, DownloadedTrack>(StringComparer.Ordinal);
        foreach (var track in tracks)
        {
            if (string.IsNullOrWhiteSpace(track.VideoId) || !HasExistingLocalFile(track.LocalFilePath))
            {
                continue;
            }

            result[track.VideoId] = track;
        }

        return result;
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

        return HasExistingLocalFile(track.LocalFilePath) ? track : null;
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

        return HasExistingLocalFile(track.LocalFilePath) ? track : null;
    }

    public Task AddDownloadedTrackAsync(DownloadedTrack track) => _repository.AddOrReplaceAsync(track);

    public async Task SaveCompletedDownloadRecordAsync(
        string videoId,
        string title,
        string localFilePath,
        bool isVideo,
        string? remoteSourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentException("VideoId is required.", nameof(videoId));
        }

        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            throw new ArgumentException("Local file path is required.", nameof(localFilePath));
        }

        var existing = await _repository.GetByVideoIdAsync(videoId);
        var previousPath = existing?.LocalFilePath;

        await _repository.AddOrReplaceAsync(new DownloadedTrack
        {
            VideoId = videoId,
            Title = title,
            Author = string.IsNullOrWhiteSpace(existing?.Author)
                ? AppGlobal.AList.UnknownArtist
                : existing.Author,
            ThumbnailUrl = ResolveThumbnailUrl(videoId, existing?.ThumbnailUrl),
            LocalFilePath = localFilePath,
            IsVideo = isVideo,
            DownloadedDate = DateTime.UtcNow,
            HasUploaded = existing?.HasUploaded ?? false,
            UploadedDate = existing?.UploadedDate,
            UploadedRemotePath = existing?.UploadedRemotePath,
            RemoteSourcePath = remoteSourcePath ?? existing?.RemoteSourcePath
        });

        await _favoriteService.UpdateLocalFilePathAsync(videoId, localFilePath);

        if (!string.IsNullOrWhiteSpace(previousPath)
            && !string.Equals(previousPath, localFilePath, StringComparison.OrdinalIgnoreCase))
        {
            FileHelp.DeleteIfExists(previousPath);
        }
    }

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

    public async Task ClearDownloadedLocalFileAsync(string videoId, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return;
        }

        FileHelp.DeleteIfExists(filePath);
        var track = await _repository.GetByVideoIdAsync(videoId);
        if (track == null)
        {
            return;
        }

        track.LocalFilePath = string.Empty;
        await _repository.AddOrReplaceAsync(track);
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

    public async Task DeleteAudioFileAsync(string filePath, string? videoId = null)
    {
        FileHelp.DeleteIfExists(filePath);
        await _favoriteService.RemoveTrackByFilePathAsync(filePath);
    }

    private bool HasExistingLocalFile(string? filePath)
        => !string.IsNullOrWhiteSpace(filePath) && _fileSystem.FileExists(filePath);

    private static string? ResolveThumbnailUrl(string videoId, string? existingThumbnail)
    {
        if (!string.IsNullOrWhiteSpace(existingThumbnail))
        {
            return existingThumbnail;
        }

        if (videoId.StartsWith(AppGlobal.AList.RemoteTrackIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
    }
}
