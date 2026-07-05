using YoutubeExplode.Search;
using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface IYouTubeService
{
    IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query);
    Task<string?> GetAudioOnlyStreamUrlAsync(string videoId);
    Task<string?> GetMuxedStreamUrlAsync(string videoId);
    Task<string> DownloadAsync(string videoId, string fileName, bool isVideo, IProgress<double>? progress = null);
}
