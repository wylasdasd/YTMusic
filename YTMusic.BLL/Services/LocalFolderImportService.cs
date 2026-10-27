using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Services;

public sealed class LocalFolderImportService : ILocalFolderImportService
{
    private readonly IFavoriteRepository _repository;

    public LocalFolderImportService(IFavoriteRepository repository)
    {
        _repository = repository;
    }

    public async Task<LocalFolderImportResult?> ImportAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException("文件夹不存在。");
        }

        var root = Path.GetFullPath(directoryPath);
        var files = await Task.Run(() => LocalAudioFolder.FindAudioFiles(root)).ConfigureAwait(false);
        if (files.Count == 0)
        {
            return null;
        }

        var folders = await _repository.GetFoldersAsync().ConfigureAwait(false);
        var name = LocalAudioFolder.AllocateFolderName(
            new DirectoryInfo(root).Name,
            folders.Select(folder => folder.Name).Append("已下载"));
        var folder = await _repository.CreateLocalImportFolderAsync(name, root).ConfigureAwait(false);
        var addedDate = DateTime.UtcNow;
        var tracks = files.Select(path => new FavoriteTrack
        {
            VideoId = LocalAudioFolder.CreateVideoId(path),
            FolderId = folder.Id,
            Title = LocalAudioFolder.TrackTitle(path),
            Author = string.Empty,
            AddedDate = addedDate,
            LocalFilePath = path
        }).ToArray();
        await _repository.AddTracksAsync(tracks).ConfigureAwait(false);
        return new LocalFolderImportResult
        {
            Folder = folder,
            TrackCount = tracks.Length
        };
    }
}
