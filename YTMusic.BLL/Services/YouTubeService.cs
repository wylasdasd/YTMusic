using System.IO;
using CommonTool.FileHelps;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Abstractions;
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
        var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        return streamInfo?.Url;
    }

    public async Task<string?> GetMuxedStreamUrlAsync(string videoId)
    {
        var manifest = await _youTubeApiClient.GetStreamManifestAsync(videoId);
        var streamInfo = manifest.GetMuxedStreams().GetWithHighestVideoQuality();
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
        var streamManifest = await _youTubeApiClient.GetStreamManifestAsync(videoId);
        IStreamInfo? streamInfo;

        if (isVideo)
        {
            streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
        }
        else
        {
            streamInfo = streamManifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.WebM)
                .GetWithHighestBitrate() ?? streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        }

        if (streamInfo == null)
        {
            throw new InvalidOperationException($"No {(isVideo ? "video" : "audio")} stream found.");
        }

        var musicDirectory = _downloadMusicDirectoryProvider.GetDownloadedMusicDirectory();
        FileHelp.EnsureDirectoryExists(musicDirectory);

        var safeFileName = FileHelp.SafeFileName(fileName);
        var filePath = Path.Combine(musicDirectory, $"{safeFileName}.{streamInfo.Container.Name}");

        await _youTubeApiClient.DownloadStreamAsync(streamInfo, filePath, progress);

        return filePath;
    }
}
