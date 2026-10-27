using System.Net;
using System.Net.Http.Headers;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Infrastructure.YouTube;

/// <summary>基于 YoutubeExplode 的 <see cref="IYouTubeApiClient"/> 实现。</summary>
public sealed class YoutubeExplodeClient : IYouTubeApiClient
{
    /// <summary>
    /// 与 YoutubeExplode 抗限速分段长度一致。用 HTTP Range 头分段，不用它写进 URL 的 range 参数。
    /// </summary>
    private const long DownloadChunkLength = 9_898_989;

    private static readonly HttpClient DownloadHttp = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

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

    public async Task DownloadStreamAsync(IStreamInfo streamInfo, string filePath, IProgress<double>? progress = null)
    {
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                await Task.Run(() => DownloadWithRangeHeadersAsync(streamInfo, filePath, progress));
                return;
            }

            await DownloadWithRangeHeadersAsync(streamInfo, filePath, progress);
        }
        catch (Exception ex) when (IsAnonymousStreamBlocked(ex))
        {
            throw new InvalidOperationException(AppGlobal.Network.YouTubeAnonymousAccessBlocked, ex);
        }
    }

    /// <summary>
    /// YoutubeExplode 的 DownloadAsync 会把 range 写进查询串，当前 YouTube 常因此返回 403。
    /// 在线播放的本地代理用 HTTP Range 头可以拉到同一条流，下载走同一方式。
    /// </summary>
    private static async Task DownloadWithRangeHeadersAsync(
        IStreamInfo streamInfo,
        string filePath,
        IProgress<double>? progress)
    {
        var total = streamInfo.Size.Bytes;
        if (total <= 0)
        {
            throw new InvalidOperationException("音频流大小未知，无法下载。");
        }

        try
        {
            await using var destination = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

            long downloaded = 0;
            while (downloaded < total)
            {
                var end = Math.Min(downloaded + DownloadChunkLength - 1, total - 1);
                using var request = new HttpRequestMessage(HttpMethod.Get, streamInfo.Url);
                request.Headers.Range = new RangeHeaderValue(downloaded, end);

                using var response = await DownloadHttp
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if (response.StatusCode is not (HttpStatusCode.PartialContent or HttpStatusCode.OK))
                {
                    throw new InvalidOperationException($"下载音频失败（HTTP {(int)response.StatusCode}）。");
                }

                await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var buffer = new byte[81920];
                var chunkRead = 0;
                int read;
                while ((read = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    chunkRead += read;
                    downloaded += read;
                    progress?.Report(Math.Min(1d, (double)downloaded / total));
                }

                if (chunkRead == 0)
                {
                    throw new InvalidOperationException("下载音频时服务器没有返回数据。");
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }
            }

            progress?.Report(1d);
        }
        catch
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
                // 删不掉时留下残文件，下次下载会覆盖。
            }

            throw;
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
