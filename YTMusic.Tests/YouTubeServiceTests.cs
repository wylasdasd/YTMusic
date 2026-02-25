using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YoutubeExplode.Common;
using YTMusic.Services;

namespace YTMusic.Tests
{
    public class YouTubeServiceTests
    {
        private readonly IYouTubeService _youTubeService;

        public YouTubeServiceTests()
        {
            _youTubeService = new YouTubeService();
        }

        [Fact]
        public async Task SearchVideosAsync_ReturnsResults()
        {
            // Arrange
            string query = "Never gonna give you up";

            // Act
            var results = await _youTubeService.SearchVideosAsync(query).CollectAsync(20);

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.True(results.Count <= 20); // We take at most 20 in the service
        }

        [Fact]
        public async Task GetAudioOnlyStreamUrlAsync_ReturnsValidUrl()
        {
            // Arrange
            // Using a well-known video ID (Rick Astley - Never Gonna Give You Up)
            string videoId = "dQw4w9WgXcQ";

            // Act
            var url = await _youTubeService.GetAudioOnlyStreamUrlAsync(videoId);

            // Assert
            Assert.False(string.IsNullOrEmpty(url));
            Assert.Contains("http", url);
        }

        [Fact]
        public async Task GetMuxedStreamUrlAsync_ReturnsValidUrl()
        {
            // Arrange
            string videoId = "dQw4w9WgXcQ";

            // Act
            var url = await _youTubeService.GetMuxedStreamUrlAsync(videoId);

            // Assert
            Assert.False(string.IsNullOrEmpty(url));
            Assert.Contains("http", url);
        }

        [Fact]
        public async Task DownloadAsync_DownloadsFile()
        {
            // Arrange
            string videoId = "dQw4w9WgXcQ"; // Rickroll (short audio)
            string fileName = "test_download_audio";

            // Act
            var filePath = await _youTubeService.DownloadAsync(videoId, fileName, false);

            // Assert
            Assert.False(string.IsNullOrEmpty(filePath));
            Assert.True(System.IO.File.Exists(filePath));

            // Cleanup
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}