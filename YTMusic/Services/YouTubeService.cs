using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonTool.FileHelps;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YTMusic.Services
{
    public class YouTubeService : IYouTubeService
    {
        private readonly YoutubeClient _youtubeClient;

        public YouTubeService()
        {
            _youtubeClient = new YoutubeClient();
        }

        public IAsyncEnumerable<VideoSearchResult> SearchVideosAsync(string query)
        {
            return _youtubeClient.Search.GetVideosAsync(query);
        }

        public async Task<string?> GetAudioOnlyStreamUrlAsync(string videoId)
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            return audioStreamInfo?.Url;
        }

        public async Task<string?> GetMuxedStreamUrlAsync(string videoId)
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            var muxedStreamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            return muxedStreamInfo?.Url;
        }

        public async Task<string> DownloadAsync(string videoId, string fileName, bool isVideo, System.IProgress<double>? progress = null)
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            IStreamInfo? streamInfo;

            if (isVideo)
            {
                streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            }
            else
            {
                streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            }

            if (streamInfo == null)
            {
                throw new InvalidOperationException($"No {(isVideo ? "video" : "audio")} stream found.");
            }

            string musicDirectory = StoragePaths.GetDownloadedMusicDirectory();
            FileHelp.EnsureDirectoryExists(musicDirectory);

            string safeFileName = FileHelp.SafeFileName(fileName);
            string filePath = Path.Combine(musicDirectory, $"{safeFileName}.{streamInfo.Container.Name}");

            await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);

            return filePath;
        }
    }
}