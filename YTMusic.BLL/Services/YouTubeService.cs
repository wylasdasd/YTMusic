using System.IO;
using CommonTool.FileHelps;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services;

public sealed class YouTubeService : IYouTubeService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly IDownloadMusicDirectoryProvider _downloadMusicDirectoryProvider;

    public YouTubeService(IDownloadMusicDirectoryProvider downloadMusicDirectoryProvider)
    {
        _youtubeClient = new YoutubeClient();
        _downloadMusicDirectoryProvider = downloadMusicDirectoryProvider;
    }

    public IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query)
        => _youtubeClient.Search.GetVideosAsync(query);

    public async Task<string?> GetAudioOnlyStreamUrlAsync(string videoId)
    {
        if (OperatingSystem.IsAndroid())
        {
            return await Task.Run(async () =>
            {
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                return audioStreamInfo?.Url;
            });
        }

        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
        var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        return streamInfo?.Url;
    }

    public async Task<string?> GetMuxedStreamUrlAsync(string videoId)
    {
        if (OperatingSystem.IsAndroid())
        {
            return await Task.Run(async () =>
            {
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                var muxedStreamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                return muxedStreamInfo?.Url;
            });
        }

        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
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
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
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

        await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);

        return filePath;
    }
}
