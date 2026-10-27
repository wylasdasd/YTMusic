using YTMusic.BLL.Abstractions;

namespace YTMusic.Services;

/// <summary>
/// 删除本地文件后的统一入口：写库/删盘由 BLL 完成，播放列表与续播由 <see cref="MusicPlayerService"/> 处理。
/// </summary>
public sealed class LocalFileDeleteCoordinator
{
    private readonly MusicPlayerService _playerService;

    public LocalFileDeleteCoordinator(MusicPlayerService playerService)
    {
        _playerService = playerService;
    }

    public Task NotifyLocalFileRemovedAsync(string? localFilePath)
        => _playerService.ApplyLocalFileDeletedAsync(localFilePath);

    public async Task RemoveDownloadedTrackAsync(
        ILocalMusicService localMusic,
        IFavoriteService favorites,
        string videoId,
        string? localFilePath,
        bool removeFavoriteByPath = true)
    {
        var path = localFilePath;
        if (removeFavoriteByPath && !string.IsNullOrWhiteSpace(path))
        {
            await favorites.RemoveTrackByFilePathAsync(path);
        }

        await localMusic.RemoveDownloadedTrackAsync(videoId, path ?? string.Empty);
        await NotifyLocalFileRemovedAsync(path);
    }

    public async Task ClearDownloadedLocalFileAsync(
        ILocalMusicService localMusic,
        string videoId,
        string? localFilePath)
    {
        var path = localFilePath;
        await localMusic.ClearDownloadedLocalFileAsync(videoId, path);
        await NotifyLocalFileRemovedAsync(path);
    }

    public async Task DeleteAudioFileAsync(
        ILocalMusicService localMusic,
        string? localFilePath,
        string? videoId = null)
    {
        var path = localFilePath;
        await localMusic.DeleteAudioFileAsync(path ?? string.Empty, videoId);
        await NotifyLocalFileRemovedAsync(path);
    }
}
