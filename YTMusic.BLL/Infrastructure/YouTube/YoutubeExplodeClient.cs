using YoutubeExplode;
using YoutubeExplode.Exceptions;
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
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                return await Task.Run(async () => await _client.Videos.Streams.GetManifestAsync(videoId));
            }

            return await _client.Videos.Streams.GetManifestAsync(videoId);
        }
        catch (Exception ex) when (IsAnonymousStreamBlocked(ex))
        {
            throw new InvalidOperationException(
                "视频可以在网页打开，但 YouTube 拒绝了匿名取流，要求登录以确认不是机器人。",
                ex);
        }
    }

    public async Task DownloadStreamAsync(IStreamInfo streamInfo, string filePath, IProgress<double>? progress = null)
    {
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                await Task.Run(async () => await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress));
                return;
            }

            await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);
        }
        catch (Exception ex) when (IsAnonymousStreamBlocked(ex))
        {
            throw new InvalidOperationException(
                "视频可以在网页打开，但 YouTube 拒绝了匿名取流，要求登录以确认不是机器人。",
                ex);
        }
    }

    private static bool IsAnonymousStreamBlocked(Exception ex)
    {
        return ex is VideoUnavailableException or VideoUnplayableException
            || ex.Message.Contains("is not available", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("is unplayable", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("not a bot", StringComparison.OrdinalIgnoreCase);
    }
}
