using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeExplode.Search;

namespace YTMusic.Services
{
    public interface IYouTubeService
    {
        IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query);
        Task<string?> GetAudioOnlyStreamUrlAsync(string videoId);
        Task<string?> GetMuxedStreamUrlAsync(string videoId);
        Task<string> DownloadAsync(string videoId, string fileName, bool isVideo, System.IProgress<double>? progress = null);
    }
}