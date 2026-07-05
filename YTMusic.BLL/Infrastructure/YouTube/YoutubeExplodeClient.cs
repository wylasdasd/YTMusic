using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Infrastructure.YouTube;

/// <summary>基于 YoutubeExplode 的 <see cref="IYouTubeApiClient"/> 实现。</summary>
public sealed class YoutubeExplodeClient : IYouTubeApiClient
{
    private readonly YoutubeClient _client = new();

    public IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query)
        => _client.Search.GetVideosAsync(query);

    public async Task<StreamManifest> GetStreamManifestAsync(string videoId)
    {
        if (OperatingSystem.IsAndroid())
        {
            return await Task.Run(async () => await _client.Videos.Streams.GetManifestAsync(videoId));
        }

        return await _client.Videos.Streams.GetManifestAsync(videoId);
    }

    public async Task DownloadStreamAsync(IStreamInfo streamInfo, string filePath, IProgress<double>? progress = null)
    {
        if (OperatingSystem.IsAndroid())
        {
            await Task.Run(async () => await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress));
            return;
        }

        await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);
    }
}
