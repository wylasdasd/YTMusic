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
            throw new InvalidOperationException(AppGlobal.Network.YouTubeAnonymousAccessBlocked, ex);
        }
    }

    public async Task DownloadStreamAsync(
        IStreamInfo streamInfo,
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                await Task.Run(
                    async () => await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress, cancellationToken),
                    cancellationToken);
                return;
            }

            await _client.Videos.Streams.DownloadAsync(streamInfo, filePath, progress, cancellationToken);
        }
        catch (Exception ex) when (IsAnonymousStreamBlocked(ex))
        {
            throw new InvalidOperationException(AppGlobal.Network.YouTubeAnonymousAccessBlocked, ex);
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
