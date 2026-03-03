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
            if (OperatingSystem.IsAndroid())
            {
                return await Task.Run(async () =>
                {
                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    return audioStreamInfo?.Url;
                });
            }

            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            return streamInfo?.Url;
        }

        public async Task<string?> GetMuxedStreamUrlAsync(string videoId)
        {
            if (OperatingSystem.IsAndroid())
            {
                return await Task.Run(async () =>
                {
                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    var muxedStreamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                    return muxedStreamInfo?.Url;
                });
            }

            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = manifest.GetMuxedStreams().GetWithHighestVideoQuality();
            return streamInfo?.Url;
        }

        public async Task<string> DownloadAsync(string videoId, string fileName, bool isVideo, System.IProgress<double>? progress = null)
        {
            if (OperatingSystem.IsAndroid())
            {
                // YoutubeExplode 在 Android 某些路径会命中同步网络 API，必须避开主线程。
                return await Task.Run(() => DownloadCoreAsync(videoId, fileName, isVideo, progress));
            }

            return await DownloadCoreAsync(videoId, fileName, isVideo, progress);
        }

        private async Task<string> DownloadCoreAsync(string videoId, string fileName, bool isVideo, System.IProgress<double>? progress)
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
