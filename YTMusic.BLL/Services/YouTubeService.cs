using System.IO;
using CommonHelp;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Infrastructure.YouTube;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services;

public sealed class YouTubeService : IYouTubeService
{
    private readonly IYouTubeApiClient _youTubeApiClient;
    private readonly IDownloadMusicDirectoryProvider _downloadMusicDirectoryProvider;

    public YouTubeService(
        IYouTubeApiClient youTubeApiClient,
        IDownloadMusicDirectoryProvider downloadMusicDirectoryProvider)
    {
        _youTubeApiClient = youTubeApiClient;
        _downloadMusicDirectoryProvider = downloadMusicDirectoryProvider;
    }

    public IAsyncEnumerable<YoutubeExplode.Search.VideoSearchResult> SearchVideosAsync(string query)
        => _youTubeApiClient.SearchVideosAsync(query);

    public async Task<string?> GetAudioOnlyStreamUrlAsync(string videoId)
    {
        var manifest = await _youTubeApiClient.GetStreamManifestAsync(videoId);
        var streamInfo = YouTubeStreamSelection.SelectPreferredAudioStream(manifest);
        return streamInfo?.Url;
    }

    public async Task<string> DownloadAsync(string videoId, string fileName, bool isVideo, IProgress<double>? progress = null)
    {
        if (OperatingSystem.IsAndroid())
        {
            return await Task.Run(() => DownloadCoreAsync(videoId, fileName, isVideo, progress));
        }

        return await DownloadCoreAsync(videoId, fileName, isVideo, progress);
    }

    private async Task<string> DownloadCoreAsync(string videoId, string fileName, bool isVideo, IProgress<double>? progress)
    {
        var musicDirectory = _downloadMusicDirectoryProvider.GetDownloadedMusicDirectory();
        FileHelp.EnsureDirectoryExists(musicDirectory + Path.DirectorySeparatorChar);

        var safeFileName = FileHelp.SafeFileName(fileName);

        if (isVideo)
        {
            throw new InvalidOperationException(AppGlobal.Network.VideoDownloadNotSupported);
        }

        var streamManifest = await _youTubeApiClient.GetStreamManifestAsync(videoId);
        var streamInfo = YouTubeStreamSelection.SelectPreferredAudioStream(streamManifest);

        if (streamInfo == null)
        {
            throw new InvalidOperationException("未找到可下载的音频流。");
        }

        var filePath = Path.Combine(musicDirectory, $"{safeFileName}.{streamInfo.Container.Name}");

        await _youTubeApiClient.DownloadStreamAsync(streamInfo, filePath, progress);

        return filePath;
    }
}
