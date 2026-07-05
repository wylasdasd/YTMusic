using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YoutubeExplode.Common;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.BLL.Services;

namespace YTMusic.Tests
{
    public class YouTubeServiceTests
    {
        private readonly IYouTubeService _youTubeService;

        public YouTubeServiceTests()
        {
            _youTubeService = new YouTubeService(new TestDownloadMusicDirectoryProvider());
        }

        [Fact]
        public async Task SearchVideosAsync_ReturnsResults()
        {
            string query = "Never gonna give you up";
            var results = await _youTubeService.SearchVideosAsync(query).CollectAsync(20);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.True(results.Count <= 20);
        }

        [Fact]
        public async Task GetAudioOnlyStreamUrlAsync_ReturnsValidUrl()
        {
            string videoId = "dQw4w9WgXcQ";
            var url = await _youTubeService.GetAudioOnlyStreamUrlAsync(videoId);

            Assert.False(string.IsNullOrEmpty(url));
            Assert.Contains("http", url);
        }

        [Fact]
        public async Task GetMuxedStreamUrlAsync_ReturnsValidUrl()
        {
            string videoId = "dQw4w9WgXcQ";
            var url = await _youTubeService.GetMuxedStreamUrlAsync(videoId);

            Assert.False(string.IsNullOrEmpty(url));
            Assert.Contains("http", url);
        }

        [Fact]
        public async Task DownloadAsync_DownloadsFile()
        {
            string videoId = "dQw4w9WgXcQ";
            string fileName = "test_download_audio";

            var filePath = await _youTubeService.DownloadAsync(videoId, fileName, false);

            Assert.False(string.IsNullOrEmpty(filePath));
            Assert.True(System.IO.File.Exists(filePath));

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private sealed class TestDownloadMusicDirectoryProvider : IDownloadMusicDirectoryProvider
        {
            public string GetDownloadedMusicDirectory() => Path.Combine(Path.GetTempPath(), "YTMusicTests");

            public string ResolveLocalDownloadDirectory(IReadOnlyList<string>? folderNames)
                => GetDownloadedMusicDirectory();
        }
    }
}
