using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace YTMusic.BLL.Ports;

/// <summary>YouTube 流媒体 API 抽象（YoutubeExplode 实现）。</summary>
public interface IYouTubeApiClient
{
    IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query);

    Task<StreamManifest> GetStreamManifestAsync(string videoId);

    Task DownloadStreamAsync(IStreamInfo streamInfo, string filePath, IProgress<double>? progress = null);
}
