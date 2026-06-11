using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using CommonTool.TimeHelps;
using CommonTool.ArrayHelps;

namespace YTMusic.Services
{
    public class PlayingItem
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? LocalFilePath { get; set; }
        public bool? IsVideo { get; set; }
        public double? DurationSeconds { get; set; }
    }

    public class PlaybackHistoryItem : PlayingItem
    {
        public DateTime PlayedAtUtc { get; set; }
    }

    public class LocalAudioProxy : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly int _port;
        private static readonly HttpClient _httpClient = new HttpClient();

        public string ProxyUrl { get; private set; }
        public IStreamInfo? CurrentStreamInfo { get; set; }
        public string ContentType { get; set; } = "audio/mp4";

        public LocalAudioProxy()
        {
            _cts = new CancellationTokenSource();
            
            var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            _port = ((System.Net.IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            Task.Run(AcceptConnections);
            ProxyUrl = $"http://127.0.0.1:{_port}/stream";
        }

        private async Task AcceptConnections()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch { }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var streamInfo = CurrentStreamInfo;
                if (streamInfo == null)
                {
                    PlaybackDiagnostics.LogError($"Proxy {ContentType} request with no CurrentStreamInfo");
                    response.StatusCode = 404;
                    return;
                }

                PlaybackDiagnostics.Log(
                    $"Proxy {ContentType} {request.HttpMethod} range={request.Headers["Range"] ?? "none"} upstream={PlaybackDiagnostics.DescribeUrl(streamInfo.Url)}");

                response.ContentType = ContentType;
                response.AddHeader("Accept-Ranges", "bytes");
                response.AddHeader("Access-Control-Allow-Origin", "*");

                long start = 0;
                long end = streamInfo.Size.Bytes - 1;
                bool isRange = false;

                if (streamInfo == null)
                {
                    response.StatusCode = 404;
                    return;
                }

                var rangeHeader = request.Headers["Range"];
                if (rangeHeader != null)
                {
                    var range = rangeHeader.Replace("bytes=", "").Split('-');
                    start = long.Parse(range[0]);
                    if (range.Length > 1 && !string.IsNullOrEmpty(range[1]))
                    {
                        end = long.Parse(range[1]);
                    }
                    response.StatusCode = 206;
                    response.AddHeader("Content-Range", $"bytes {start}-{end}/{streamInfo.Size.Bytes}");
                    isRange = true;
                }
                else
                {
                    response.StatusCode = 200;
                }

                long length = end - start + 1;
                response.ContentLength64 = length;

                if (request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, streamInfo.Url);
                
                if (isRange)
                {
                    requestMessage.Headers.Range = new RangeHeaderValue(start, end);
                }
                
                using var upstreamResponse = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                
                // Propagate actual headers from upstream if possible
                if (upstreamResponse.Content.Headers.ContentType != null)
                    response.ContentType = upstreamResponse.Content.Headers.ContentType.ToString();
                
                if (upstreamResponse.Content.Headers.ContentLength.HasValue)
                    response.ContentLength64 = upstreamResponse.Content.Headers.ContentLength.Value;

                if (upstreamResponse.StatusCode == HttpStatusCode.PartialContent && upstreamResponse.Content.Headers.ContentRange != null)
                {
                    response.StatusCode = (int)HttpStatusCode.PartialContent;
                    response.AddHeader("Content-Range", upstreamResponse.Content.Headers.ContentRange.ToString());
                }
                else
                {
                    response.StatusCode = (int)upstreamResponse.StatusCode;
                }

                using var youtubeStream = await upstreamResponse.Content.ReadAsStreamAsync(_cts.Token);

                var buffer = new byte[81920];
                int read;
                while ((read = await youtubeStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0 && !_cts.IsCancellationRequested)
                {
                    await response.OutputStream.WriteAsync(buffer, 0, read);
                }
            }
            catch (Exception)
            {
                // Client disconnected or stream aborted, perfectly normal during seeking/closing
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
    }

    public class LocalFileProxy : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly int _port;

        public string ProxyUrl { get; private set; }
        public string? CurrentFilePath { get; set; }
        public string ContentType { get; set; } = "audio/mp4";

        public LocalFileProxy()
        {
            _cts = new CancellationTokenSource();
            
            var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            _port = ((System.Net.IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            Task.Run(AcceptConnections);
            ProxyUrl = $"http://127.0.0.1:{_port}/stream";
        }

        private async Task AcceptConnections()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch { }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (string.IsNullOrEmpty(CurrentFilePath) || !File.Exists(CurrentFilePath))
                {
                    response.StatusCode = 404;
                    return;
                }

                var fileInfo = new FileInfo(CurrentFilePath);
                
                response.ContentType = ContentType;
                response.AddHeader("Accept-Ranges", "bytes");
                response.AddHeader("Access-Control-Allow-Origin", "*");

                long start = 0;
                long end = fileInfo.Length - 1;

                var rangeHeader = request.Headers["Range"];
                if (rangeHeader != null)
                {
                    var range = rangeHeader.Replace("bytes=", "").Split('-');
                    start = long.Parse(range[0]);
                    if (range.Length > 1 && !string.IsNullOrEmpty(range[1]))
                    {
                        end = long.Parse(range[1]);
                    }
                    response.StatusCode = 206;
                    response.AddHeader("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
                }
                else
                {
                    response.StatusCode = 200;
                }

                long length = end - start + 1;
                response.ContentLength64 = length;

                if (request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                using var fileStream = new FileStream(CurrentFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                if (start > 0 && fileStream.CanSeek)
                {
                    fileStream.Seek(start, SeekOrigin.Begin);
                }

                var buffer = new byte[81920];
                int read;
                long totalRead = 0;
                
                while (totalRead < length && !_cts.IsCancellationRequested)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, length - totalRead);
                    read = await fileStream.ReadAsync(buffer, 0, bytesToRead, _cts.Token);
                    
                    if (read == 0) break;
                    
                    await response.OutputStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                }
            }
            catch (Exception)
            {
                // Client disconnected or stream aborted
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
    }

    public class MusicPlayerService
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly INativeAudioPlaybackService _nativeAudio;
        private readonly INativeVideoPlaybackService _nativeVideo;
        private readonly ILocalMusicService _localMusicService;
        private readonly NetworkErrorService _networkErrorService;
        private LocalAudioProxy? _proxy;
        private LocalFileProxy? _fileProxy;
        private readonly SemaphoreSlim _fileProxyInitLock = new(1, 1);
        private readonly SemaphoreSlim _audioProxyInitLock = new(1, 1);
        
        public event Action? OnChange;
        public event Action? OnTimeChanged;
        public event Action? OnRequestReplay;
        public event Action? OnRequestPause;
        public event Action? OnRequestStopWebPlayback;
        public event Action? OnRequestPlay;
        public event Action<double>? OnRequestSeek;
        public PlayingItem? CurrentVideo { get; private set; }
        public string? CurrentStreamUrl { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsSwitchingTrack { get; private set; }
        public bool UseNativePlayback => _nativeAudio.IsSupported;
        public bool IsUsingNativePlayback { get; private set; }
        // Android 视频与 Windows 一致：走 WebView + CurrentStreamUrl / LocalFileProxy，不用全屏原生 Activity。
        public bool UseNativeVideoPlayback => false;
        public bool IsUsingNativeVideoPlayback { get; private set; }
        public bool IsUsingHybridWebVideo => IsUsingNativePlayback && IsCurrentStreamVideo && CurrentStreamUrl != null;
        public bool IsCurrentStreamWebM { get; private set; }
        public bool IsCurrentStreamVideo { get; private set; }
        public double CurrentTime { get; private set; } = 0;
        public double Duration { get; private set; } = 100;
        public IReadOnlyList<PlaybackHistoryItem> PlaybackHistory => _playbackHistory;

        // Playlist State
        public List<PlayingItem> Playlist { get; private set; } = new List<PlayingItem>();
        public int CurrentPlaylistIndex { get; private set; } = -1;
        public string? CurrentPlaylistName { get; private set; }
        public enum PlaybackMode { Sequential, Random, SingleLoop }
        public PlaybackMode CurrentMode { get; private set; } = PlaybackMode.Sequential;
        private List<int> _shuffleIndices = new List<int>();
        private readonly List<PlaybackHistoryItem> _playbackHistory = new List<PlaybackHistoryItem>();

        public MusicPlayerService(INativeAudioPlaybackService nativeAudio, INativeVideoPlaybackService nativeVideo, ILocalMusicService localMusicService, NetworkErrorService networkErrorService)
        {
            _nativeAudio = nativeAudio;
            _nativeVideo = nativeVideo;
            _localMusicService = localMusicService;
            _networkErrorService = networkErrorService;
            _youtubeClient = new YoutubeClient();

            if (!_nativeAudio.IsSupported && !OperatingSystem.IsAndroid())
            {
                _proxy = new LocalAudioProxy();
                _fileProxy = new LocalFileProxy();
            }

            if (_nativeAudio.IsSupported)
            {
                _nativeAudio.PositionChanged += OnNativePositionChanged;
                _nativeAudio.PlayingStateChanged += OnNativePlayingStateChanged;
                _nativeAudio.PlaybackEnded += OnNativePlaybackEnded;
            }

            if (_nativeVideo.IsSupported)
            {
                _nativeVideo.PositionChanged += OnNativeVideoPositionChanged;
                _nativeVideo.PlayingStateChanged += OnNativeVideoPlayingStateChanged;
                _nativeVideo.PlaybackEnded += OnNativeVideoPlaybackEnded;
                _nativeVideo.PlaybackStopped += OnNativeVideoPlaybackStopped;
            }
        }

        public async Task ResetStateAsync()
        {
            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
            OnRequestPause?.Invoke();

            ClearPlaylist();
            CurrentVideo = null;
            CurrentStreamUrl = null;
            IsPlaying = false;
            IsLoading = false;
            IsUsingNativePlayback = false;
            IsUsingNativeVideoPlayback = false;
            IsCurrentStreamVideo = false;
            IsCurrentStreamWebM = false;
            CurrentTime = 0;
            Duration = 100;
            NotifyStateChanged();
            OnTimeChanged?.Invoke();
        }

        public async Task<bool> PlayAsync(VideoSearchResult video)
        {
            var item = new PlayingItem
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url,
                DurationSeconds = video.Duration?.TotalSeconds
            };

            if (!await PlayInternalAsync(item))
            {
                return false;
            }

            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            return true;
        }

        public async Task<bool> PlayAsync(IVideo video)
        {
            var item = new PlayingItem
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url,
                DurationSeconds = video.Duration?.TotalSeconds
            };

            if (!await PlayInternalAsync(item))
            {
                return false;
            }

            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            return true;
        }

        public async Task<bool> PlayAsync(PlayingItem playingItem)
        {
            if (!await PlayInternalAsync(playingItem))
            {
                return false;
            }

            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            return true;
        }

        public async Task<bool> PlayLocalFileAsync(
            string filePath,
            string title,
            bool? isVideo = null,
            string? author = null,
            string? thumbnailUrl = null,
            string? videoId = null)
        {
            var item = new PlayingItem
            {
                VideoId = !string.IsNullOrWhiteSpace(videoId) ? videoId : "local",
                Title = title,
                Author = string.IsNullOrWhiteSpace(author) ? "Local File" : author,
                ThumbnailUrl = thumbnailUrl,
                LocalFilePath = filePath,
                IsVideo = isVideo
            };

            IsLoading = true;
            IsPlaying = false;
            IsUsingNativePlayback = false;
            IsUsingNativeVideoPlayback = false;
            NotifyStateChanged();

            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                IsCurrentStreamWebM = filePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                IsCurrentStreamVideo = ShouldPlayLocalAsVideo(filePath, isVideo == true);
                if (IsCurrentStreamVideo && UseNativeVideoPlayback)
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = true;
                    CurrentStreamUrl = null;
                    CurrentTime = 0;
                    Duration = 100;
                    await _nativeVideo.PlayAsync(filePath, true, title, string.IsNullOrWhiteSpace(author) ? "Local File" : author, null);
                    CurrentVideo = item;
                    IsPlaying = true;
                    AddToPlaybackHistory(CurrentVideo, true);
                }
                else
                if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
                    IsUsingNativePlayback = true;
                    IsUsingNativeVideoPlayback = false;
                    CurrentStreamUrl = null;
                    CurrentTime = 0;
                    Duration = 100;
                    await _nativeAudio.PlayAsync(filePath, true, title, string.IsNullOrWhiteSpace(author) ? "Local File" : author, null);
                    CurrentVideo = item;
                    IsPlaying = true;
                    AddToPlaybackHistory(CurrentVideo, false);
                }
                else
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = false;
                    await EnsureFileProxyCreatedAsync();
                    _fileProxy!.ContentType = GetFileContentType(filePath, IsCurrentStreamVideo);
                    _fileProxy.CurrentFilePath = filePath;
                    CurrentStreamUrl = BuildLocalProxyStreamUrl(filePath);
                    CurrentVideo = item;
                    IsPlaying = true;
                    AddToPlaybackHistory(CurrentVideo, IsCurrentStreamVideo);
                }

                ClearPlaylist();
                CurrentMode = PlaybackMode.SingleLoop;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing local file: {ex.Message}");
                _ = _networkErrorService.NotifyFailureAsync("播放", ex);
                return false;
            }
            finally
            {
                IsLoading = false;
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// 是否显示音视频切换按钮（同步、不发起网络请求）。
        /// </summary>
        public bool CanShowVideoToggle()
        {
            var current = CurrentVideo;
            if (current == null)
            {
                return false;
            }

            if (IsCurrentStreamVideo)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(current.VideoId) && current.VideoId != "local";
        }

        public async Task<bool> CanPlayCurrentAsVideoAsync()
        {
            var current = CurrentVideo;
            if (current == null)
            {
                return false;
            }

            if (await CanPlayLocalVideoForCurrentAsync())
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            // 有 VideoId 时按钮可点；本地仅音频则需弹窗确认后拉远端流。
            return true;
        }

        public async Task<bool> CanPlayLocalVideoForCurrentAsync()
        {
            var current = CurrentVideo;
            if (current == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(current.LocalFilePath))
            {
                var localTrack = await _localMusicService.GetDownloadedTrackByFilePathAsync(current.LocalFilePath);
                if (localTrack != null && ShouldPlayLocalAsVideo(localTrack.LocalFilePath, localTrack.IsVideo))
                {
                    return true;
                }

                if (ShouldPlayLocalAsVideo(current.LocalFilePath, current.IsVideo == true))
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(current.VideoId);
            return downloaded != null
                && ShouldPlayLocalAsVideo(downloaded.LocalFilePath, downloaded.IsVideo);
        }

        public async Task<bool> CheckRemoteVideoAvailableAsync(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId) || videoId == "local")
            {
                return false;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var streams = await ResolveRemoteWebVideoStreamsAsync(videoId, cts.Token).ConfigureAwait(false);
                PlaybackDiagnostics.Log($"CheckRemoteVideo videoId={videoId} available={streams != null}");
                return streams != null;
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.LogError($"CheckRemoteVideo failed videoId={videoId}", ex);
                return false;
            }
        }

        public async Task<bool> PlayCurrentAsVideoAsync()
        {
            var current = CurrentVideo;
            if (current == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(current.LocalFilePath))
            {
                var localTrack = await _localMusicService.GetDownloadedTrackByFilePathAsync(current.LocalFilePath);
                if (localTrack != null
                    && !string.IsNullOrWhiteSpace(localTrack.LocalFilePath)
                    && ShouldPlayLocalAsVideo(localTrack.LocalFilePath, localTrack.IsVideo))
                {
                    return await PlayLocalFileAsync(localTrack.LocalFilePath, current.Title, true, current.Author, current.ThumbnailUrl);
                }
            }

            if (string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(current.VideoId);
            if (downloaded != null
                && !string.IsNullOrWhiteSpace(downloaded.LocalFilePath)
                && ShouldPlayLocalAsVideo(downloaded.LocalFilePath, downloaded.IsVideo))
            {
                return await PlayLocalFileAsync(downloaded.LocalFilePath, current.Title, true, current.Author, current.ThumbnailUrl);
            }

            return false;
        }

        public async Task<bool> PlayRemoteVideoForCurrentAsync()
        {
            var current = CurrentVideo;
            if (current == null || string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            return await PlayInternalAsync(new PlayingItem
            {
                VideoId = current.VideoId,
                Title = current.Title,
                Author = current.Author,
                ThumbnailUrl = current.ThumbnailUrl,
                DurationSeconds = current.DurationSeconds,
                IsVideo = true
            });
        }

        public async Task<bool> PlayCurrentAsAudioAsync()
        {
            var current = CurrentVideo;
            if (current == null || !IsCurrentStreamVideo)
            {
                return false;
            }

            var item = new PlayingItem
            {
                VideoId = current.VideoId,
                Title = current.Title,
                Author = current.Author,
                ThumbnailUrl = current.ThumbnailUrl,
                DurationSeconds = current.DurationSeconds,
                IsVideo = false
            };

            if (!string.IsNullOrWhiteSpace(current.VideoId) && current.VideoId != "local")
            {
                var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(current.VideoId);
                if (downloaded != null
                    && !string.IsNullOrWhiteSpace(downloaded.LocalFilePath)
                    && !ShouldPlayLocalAsVideo(downloaded.LocalFilePath, downloaded.IsVideo))
                {
                    item.LocalFilePath = downloaded.LocalFilePath;
                }
            }

            // 本地视频（VideoId=local 或仅视频下载）切音频：沿用当前文件走 audio 流，或上面已选中的纯音频下载
            if (string.IsNullOrWhiteSpace(item.LocalFilePath) && !string.IsNullOrWhiteSpace(current.LocalFilePath))
            {
                item.LocalFilePath = current.LocalFilePath;
            }

            if (string.IsNullOrWhiteSpace(item.LocalFilePath)
                && (string.IsNullOrWhiteSpace(item.VideoId) || item.VideoId == "local"))
            {
                return false;
            }

            return await PlayInternalAsync(item);
        }

        public void ClearPlaylist()
        {
            Playlist.Clear();
            _shuffleIndices.Clear();
            CurrentPlaylistIndex = -1;
            CurrentPlaylistName = null;
            NotifyStateChanged();
        }

        public void ClearPlaybackHistory()
        {
            _playbackHistory.Clear();
            NotifyStateChanged();
        }

        public async Task<bool> PlayPlaylistAsync(List<PlayingItem> items, int startIndex, string playlistName, PlaybackMode mode = PlaybackMode.Sequential)
        {
            if (items == null || items.Count == 0 || startIndex < 0 || startIndex >= items.Count)
                return false;

            if (!await PlayInternalAsync(items[startIndex]))
                return false;

            Playlist = items;
            CurrentPlaylistName = playlistName;
            CurrentPlaylistIndex = startIndex;
            CurrentMode = mode;
            GenerateShuffleIndices();
            return true;
        }

        private void GenerateShuffleIndices()
        {
            _shuffleIndices.Clear();

            var indices = Enumerable.Range(0, Playlist.Count).ToList();
            
            if (CurrentPlaylistIndex >= 0 && CurrentPlaylistIndex < Playlist.Count)
            {
                indices.RemoveAt(CurrentPlaylistIndex);
                _shuffleIndices.Add(CurrentPlaylistIndex);
            }

            indices.Shuffle();
            _shuffleIndices.AddRange(indices);
        }

        public async Task PlayNextAsync()
        {
            if (Playlist.Count == 0 || IsSwitchingTrack) return;

            int nextIndex = GetNextIndex();
            if (nextIndex == -1) return;

            int previousIndex = CurrentPlaylistIndex;
            CurrentPlaylistIndex = nextIndex;
            if (!await PlayInternalAsync(Playlist[CurrentPlaylistIndex]))
            {
                CurrentPlaylistIndex = previousIndex;
            }
        }

        private async Task<bool> TryRestartCurrentLocalWebAsync()
        {
            var current = CurrentVideo;
            if (current == null
                || string.IsNullOrWhiteSpace(current.LocalFilePath)
                || IsUsingNativePlayback
                || IsUsingNativeVideoPlayback
                || CurrentStreamUrl == null)
            {
                return false;
            }

            if (!File.Exists(current.LocalFilePath))
            {
                return false;
            }

            await EnsureFileProxyCreatedAsync();
            _fileProxy!.ContentType = GetFileContentType(current.LocalFilePath, IsCurrentStreamVideo);
            _fileProxy.CurrentFilePath = current.LocalFilePath;
            CurrentStreamUrl = BuildLocalProxyStreamUrl(current.LocalFilePath);
            CurrentTime = 0;
            IsPlaying = true;
            NotifyStateChanged();
            return true;
        }

        public async Task PlayPreviousAsync()
        {
            if (IsSwitchingTrack)
            {
                return;
            }

            // 与「下一首」不同：已播放超过阈值则从头播放当前曲，否则切上一首（常见播放器行为）
            const double restartThresholdSeconds = 3;
            if (CurrentTime > restartThresholdSeconds)
            {
                if (await TryRestartCurrentLocalWebAsync())
                {
                    return;
                }

                await SeekAsync(0);
                if (!IsPlaying)
                {
                    await ResumeAsync();
                }

                return;
            }

            if (Playlist.Count == 0)
            {
                if (CurrentVideo != null)
                {
                    await SeekAsync(0);
                }

                return;
            }

            int previousIndex = GetPreviousIndex();
            if (previousIndex == -1)
            {
                return;
            }

            int currentIndex = CurrentPlaylistIndex;
            CurrentPlaylistIndex = previousIndex;
            if (!await PlayInternalAsync(Playlist[CurrentPlaylistIndex]))
            {
                CurrentPlaylistIndex = currentIndex;
            }
        }

        private int GetNextIndex()
        {
            if (Playlist.Count <= 1) return CurrentPlaylistIndex;

            if (CurrentMode == PlaybackMode.Random)
            {
                int currentShufflePosition = _shuffleIndices.IndexOf(CurrentPlaylistIndex);
                if (currentShufflePosition == -1 || currentShufflePosition == _shuffleIndices.Count - 1)
                {
                    // Re-shuffle and start over
                    // Wait to play the next song in the new sequence
                    int previousTrackIndex = CurrentPlaylistIndex;
                    GenerateShuffleIndices();
                    
                    // Edge case: if the newly shuffled first item is the same as the one we just finished playing, skip to the second (if size > 1)
                    if (_shuffleIndices.Count > 1 && _shuffleIndices[0] == previousTrackIndex)
                    {
                        return _shuffleIndices[1];
                    }
                    return _shuffleIndices[0];
                }
                return _shuffleIndices[currentShufflePosition + 1];
            }
            
            // Sequential
            return (CurrentPlaylistIndex + 1) % Playlist.Count;
        }

        private int GetPreviousIndex()
        {
            if (Playlist.Count <= 1) return CurrentPlaylistIndex;

            if (CurrentMode == PlaybackMode.Random)
            {
                int currentShufflePosition = _shuffleIndices.IndexOf(CurrentPlaylistIndex);
                if (currentShufflePosition > 0)
                {
                    return _shuffleIndices[currentShufflePosition - 1];
                }
                // If at the beginning of shuffle, wrap to end
                return _shuffleIndices[^1];
            }

            // Sequential
            int previousIndex = CurrentPlaylistIndex - 1;
            if (previousIndex < 0)
            {
                previousIndex = Playlist.Count - 1;
            }
            return previousIndex;
        }

        public void TogglePlaybackMode()
        {
            if (Playlist.Count == 0) return;

            CurrentMode = CurrentMode switch
            {
                PlaybackMode.Sequential => PlaybackMode.Random,
                PlaybackMode.Random => PlaybackMode.SingleLoop,
                PlaybackMode.SingleLoop => PlaybackMode.Sequential,
                _ => PlaybackMode.Sequential
            };

            if (CurrentMode == PlaybackMode.Random && Playlist.Count > 0)
            {
                // Re-initialize random sequence starting from the currently playing
                GenerateShuffleIndices();
            }

            NotifyStateChanged();
        }

        public async Task OnTrackEndedAsync()
        {
            if (CurrentMode == PlaybackMode.SingleLoop)
            {
                if (IsUsingNativePlayback)
                {
                    await _nativeAudio.SeekAsync(0);
                    await _nativeAudio.ResumeAsync();
                }
                else if (IsUsingNativeVideoPlayback)
                {
                    await _nativeVideo.SeekAsync(0);
                    await _nativeVideo.ResumeAsync();
                }
                else
                {
                    // Re-play current without full reload
                    OnRequestReplay?.Invoke();
                }
                return;
            }

            SetPlayingState(false);

            if (Playlist.Count > 0)
            {
                await PlayNextAsync();
            }
        }

        private async Task<bool> PlayInternalAsync(PlayingItem video)
        {
            var isTrackSwitch = CurrentVideo != null;
            var shouldAutoPlay = isTrackSwitch ? IsPlaying : true;
            try
            {
                if (isTrackSwitch)
                {
                    IsSwitchingTrack = true;
                }
                else
                {
                    IsLoading = true;
                    IsPlaying = false;
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = false;
                }

                NotifyStateChanged();

                if (!string.IsNullOrEmpty(video.LocalFilePath))
                {
                    if (!File.Exists(video.LocalFilePath))
                    {
                        return false;
                    }

                    IsCurrentStreamWebM = video.LocalFilePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                    IsCurrentStreamVideo = ShouldPlayLocalAsVideo(video.LocalFilePath, video.IsVideo == true);

                    if (IsCurrentStreamVideo && UseNativeVideoPlayback)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = true;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeVideo.PlayAsync(video.LocalFilePath, true, video.Title, video.Author, video.DurationSeconds);
                        CurrentVideo = video;
                        IsPlaying = true;
                        AddToPlaybackHistory(video, true);
                    }
                    else if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                    {
                        await StartNativeLocalPlaybackAsync(video.LocalFilePath, video, shouldAutoPlay);
                        CurrentVideo = video;
                        IsPlaying = shouldAutoPlay;
                        AddToPlaybackHistory(video, false);
                    }
                    else
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        await EnsureFileProxyCreatedAsync();
                        _fileProxy!.ContentType = GetFileContentType(video.LocalFilePath, IsCurrentStreamVideo);
                        _fileProxy.CurrentFilePath = video.LocalFilePath;
                        CurrentStreamUrl = BuildLocalProxyStreamUrl(video.LocalFilePath);
                        CurrentVideo = video;
                        IsPlaying = shouldAutoPlay;
                        AddToPlaybackHistory(video, IsCurrentStreamVideo);
                    }
                    return true;
                }

                // Prefer downloaded local files when we have a record for this video id.
                var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(video.VideoId);
                var useDownloadedLocal = downloaded != null && !string.IsNullOrWhiteSpace(downloaded.LocalFilePath);
                if (video.IsVideo == true)
                {
                    // 显式请求视频时：纯音频下载、或非 .mp4 的“视频”下载，改走在线视频流。
                    useDownloadedLocal = useDownloadedLocal
                        && ShouldPlayLocalAsVideo(downloaded!.LocalFilePath, downloaded.IsVideo);
                    if (downloaded != null && !useDownloadedLocal)
                    {
                        PlaybackDiagnostics.Log(
                            $"PlayInternal skip local for remote video videoId={video.VideoId} downloadedIsVideo={downloaded.IsVideo} path={downloaded.LocalFilePath}");
                    }
                }

                if (useDownloadedLocal)
                {
                    PlaybackDiagnostics.Log(
                        $"PlayInternal using local download videoId={video.VideoId} requestVideo={video.IsVideo == true} downloadedIsVideo={downloaded!.IsVideo}");
                    video.LocalFilePath = downloaded.LocalFilePath;
                    video.IsVideo = downloaded.IsVideo;
                    IsCurrentStreamWebM = video.LocalFilePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                    IsCurrentStreamVideo = ShouldPlayLocalAsVideo(video.LocalFilePath, downloaded.IsVideo);

                    if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                    {
                        await StartNativeLocalPlaybackAsync(video.LocalFilePath, video, shouldAutoPlay);
                        AddToPlaybackHistory(video, false);
                    }
                    else if (IsCurrentStreamVideo && UseNativeVideoPlayback)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = true;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeVideo.PlayAsync(video.LocalFilePath, true, video.Title, video.Author, video.DurationSeconds);
                        AddToPlaybackHistory(video, true);
                    }
                    else
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        await EnsureFileProxyCreatedAsync();
                        _fileProxy!.ContentType = GetFileContentType(video.LocalFilePath, IsCurrentStreamVideo);
                        _fileProxy.CurrentFilePath = video.LocalFilePath;
                        CurrentStreamUrl = BuildLocalProxyStreamUrl(video.LocalFilePath);
                        AddToPlaybackHistory(video, IsCurrentStreamVideo);
                    }

                    CurrentVideo = video;
                    IsPlaying = shouldAutoPlay;
                    return true;
                }

                using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                if (video.IsVideo == true)
                {
                    PlaybackDiagnostics.Log($"PlayRemoteVideo start videoId={video.VideoId} isTrackSwitch={isTrackSwitch}");
                    var webVideo = await ResolveRemoteWebVideoStreamsAsync(video.VideoId, streamCts.Token).ConfigureAwait(false);

                    if (webVideo != null)
                    {
                        var videoStreamInfo = webVideo.VideoStream;
                        var companionAudio = webVideo.CompanionAudioStream;
                        IsCurrentStreamWebM = videoStreamInfo.Container == Container.WebM;
                        IsCurrentStreamVideo = true;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = await BuildWebVideoStreamUrlAsync(videoStreamInfo).ConfigureAwait(false);

                        if (companionAudio != null)
                        {
                            PlaybackDiagnostics.Log(
                                $"PlayRemoteVideo hybrid video={videoStreamInfo.Container} audio={companionAudio.Container} webUrl={PlaybackDiagnostics.DescribeUrl(CurrentStreamUrl)}");
                            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
                            IsUsingNativePlayback = true;
                            CurrentTime = 0;
                            Duration = 100;
                            await _nativeAudio.PlayAsync(companionAudio.Url, false, video.Title, video.Author, video.DurationSeconds);
                            if (!shouldAutoPlay)
                            {
                                await _nativeAudio.PauseAsync();
                            }

                            PlaybackDiagnostics.Log($"PlayRemoteVideo native audio started url={PlaybackDiagnostics.DescribeUrl(companionAudio.Url)}");
                        }
                        else
                        {
                            PlaybackDiagnostics.Log(
                                $"PlayRemoteVideo muxed/web-only video={videoStreamInfo.Container} webUrl={PlaybackDiagnostics.DescribeUrl(CurrentStreamUrl)}");
                            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                            IsUsingNativePlayback = false;
                        }

                        CurrentVideo = video;
                        IsPlaying = shouldAutoPlay;
                        AddToPlaybackHistory(video, true);
                        PlaybackDiagnostics.Log(
                            $"PlayRemoteVideo success hybrid={companionAudio != null} IsCurrentStreamVideo={IsCurrentStreamVideo} IsUsingNativePlayback={IsUsingNativePlayback}");
                        return true;
                    }

                    PlaybackDiagnostics.LogError($"PlayRemoteVideo no stream videoId={video.VideoId}");
                    if (video.IsVideo == true)
                    {
                        await _networkErrorService.NotifyFailureAsync("播放");
                        return false;
                    }
                }

                var streamInfo = await GetPreferredAudioStreamInfoAsync(video.VideoId, streamCts.Token);

                if (streamInfo != null)
                {
                    IsCurrentStreamWebM = streamInfo.Container.Name.ToLower().Contains("webm");
                    IsCurrentStreamVideo = false;

                    if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = true;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeAudio.PlayAsync(streamInfo.Url, false, video.Title, video.Author, video.DurationSeconds);
                        AddToPlaybackHistory(video, false);
                    }
                    else if (OperatingSystem.IsAndroid())
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = streamInfo.Url;
                        AddToPlaybackHistory(video, false);
                    }
                    else
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        EnsureProxiesCreated();
                        _proxy!.ContentType = GetStreamContentType(streamInfo, IsCurrentStreamVideo);
                        _proxy.CurrentStreamInfo = streamInfo;
                        CurrentStreamUrl = $"{_proxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                        AddToPlaybackHistory(video, false);
                    }

                    CurrentVideo = video;
                    IsPlaying = true;
                    return true;
                }

                await _networkErrorService.NotifyFailureAsync("播放");
                return false;
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.LogError($"PlayInternal failed videoId={video.VideoId} isVideo={video.IsVideo == true}", ex);
                await _networkErrorService.NotifyFailureAsync("播放", ex);
                if (video.IsVideo == true || !isTrackSwitch)
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                    IsPlaying = false;
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = false;
                    if (video.IsVideo == true)
                    {
                        IsCurrentStreamVideo = false;
                        CurrentStreamUrl = null;
                    }
                }

                return false;
            }
            finally
            {
                IsLoading = false;
                IsSwitchingTrack = false;
                NotifyStateChanged();
            }
        }

        public void SetPlayingState(bool isPlaying)
        {
            IsPlaying = isPlaying;
            NotifyStateChanged();
        }

        public async Task PauseAsync()
        {
            SetPlayingState(false);

            if (IsUsingHybridWebVideo)
            {
                await _nativeAudio.PauseAsync();
                OnRequestPause?.Invoke();
                return;
            }

            if (IsUsingNativePlayback)
            {
                await _nativeAudio.PauseAsync();
            }
            else if (IsUsingNativeVideoPlayback)
            {
                await _nativeVideo.PauseAsync();
            }
            else
            {
                OnRequestPause?.Invoke();
            }
        }

        public async Task ResumeAsync()
        {
            if (!IsUsingNativePlayback && !IsUsingNativeVideoPlayback && CurrentStreamUrl == null && CurrentVideo != null)
            {
                var resumeItem = new PlayingItem
                {
                    VideoId = CurrentVideo.VideoId,
                    Title = CurrentVideo.Title,
                    Author = CurrentVideo.Author,
                    ThumbnailUrl = CurrentVideo.ThumbnailUrl,
                    LocalFilePath = CurrentVideo.LocalFilePath,
                    DurationSeconds = CurrentVideo.DurationSeconds,
                    IsVideo = IsCurrentStreamVideo ? true : false
                };

                await PlayInternalAsync(resumeItem);
                return;
            }

            SetPlayingState(true);

            if (IsUsingHybridWebVideo)
            {
                await _nativeAudio.ResumeAsync();
                OnRequestPlay?.Invoke();
                return;
            }

            if (IsUsingNativePlayback)
            {
                await _nativeAudio.ResumeAsync();
            }
            else if (IsUsingNativeVideoPlayback)
            {
                await _nativeVideo.ResumeAsync();
            }
            else
            {
                OnRequestPlay?.Invoke();
            }
        }

        public async Task SeekAsync(double positionSeconds)
        {
            CurrentTime = positionSeconds;

            if (IsUsingHybridWebVideo)
            {
                await _nativeAudio.SeekAsync(positionSeconds);
                OnRequestSeek?.Invoke(positionSeconds);
                OnTimeChanged?.Invoke();
                return;
            }

            if (IsUsingNativePlayback)
            {
                await _nativeAudio.SeekAsync(positionSeconds);
                OnTimeChanged?.Invoke();
            }
            else if (IsUsingNativeVideoPlayback)
            {
                await _nativeVideo.SeekAsync(positionSeconds);
                OnTimeChanged?.Invoke();
            }
            else
            {
                OnRequestSeek?.Invoke(positionSeconds);
            }
        }

        public void UpdateTime(double currentTime)
        {
            CurrentTime = currentTime;
            OnTimeChanged?.Invoke();
        }

        public void UpdateDuration(double duration)
        {
            Duration = duration > 0 ? duration : 100;
            OnTimeChanged?.Invoke();
        }

        private void EnsureProxiesCreated()
        {
            _proxy ??= new LocalAudioProxy();
            _fileProxy ??= new LocalFileProxy();
        }

        private async Task EnsureAudioProxyCreatedAsync()
        {
            if (_proxy != null)
            {
                return;
            }

            await _audioProxyInitLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_proxy != null)
                {
                    return;
                }

                if (OperatingSystem.IsAndroid())
                {
                    _proxy = await Task.Run(() => new LocalAudioProxy()).ConfigureAwait(false);
                }
                else
                {
                    _proxy = new LocalAudioProxy();
                }

                PlaybackDiagnostics.Log($"Audio proxy ready url={_proxy.ProxyUrl}");
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.LogError("Audio proxy init failed", ex);
                throw;
            }
            finally
            {
                _audioProxyInitLock.Release();
            }
        }

        private async Task EnsureFileProxyCreatedAsync()
        {
            if (_fileProxy != null)
            {
                return;
            }

            await _fileProxyInitLock.WaitAsync();
            try
            {
                if (_fileProxy != null)
                {
                    return;
                }

                if (OperatingSystem.IsAndroid())
                {
                    _fileProxy = await Task.Run(() => new LocalFileProxy());
                }
                else
                {
                    _fileProxy = new LocalFileProxy();
                }
            }
            finally
            {
                _fileProxyInitLock.Release();
            }
        }

        private sealed class RemoteWebVideoStreams
        {
            public IStreamInfo VideoStream { get; init; } = null!;
            public IStreamInfo? CompanionAudioStream { get; init; }
        }

        private async Task<StreamManifest> GetStreamManifestAsync(string videoId, CancellationToken cancellationToken = default)
        {
            if (OperatingSystem.IsAndroid())
            {
                return await Task.Run(async () =>
                    await _youtubeClient.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false)
                ).ConfigureAwait(false);
            }

            return await _youtubeClient.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
        }

        private static IStreamInfo? SelectPreferredAudioStream(StreamManifest manifest)
        {
            return manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.WebM)
                .GetWithHighestBitrate() ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        }

        private async Task<RemoteWebVideoStreams?> ResolveRemoteWebVideoStreamsAsync(string videoId, CancellationToken cancellationToken = default)
        {
            var manifest = await GetStreamManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
            var muxedCount = manifest.GetMuxedStreams().Count();
            var videoOnlyCount = manifest.GetVideoOnlyStreams().Count();
            var audioOnlyCount = manifest.GetAudioOnlyStreams().Count();
            PlaybackDiagnostics.Log($"ResolveStreams videoId={videoId} muxed={muxedCount} videoOnly={videoOnlyCount} audioOnly={audioOnlyCount}");

            var muxed = manifest.GetMuxedStreams().GetWithHighestVideoQuality();
            if (muxed != null)
            {
                PlaybackDiagnostics.Log($"ResolveStreams using muxed container={muxed.Container}");
                return new RemoteWebVideoStreams { VideoStream = muxed, CompanionAudioStream = null };
            }

            var videoOnly = manifest.GetVideoOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality()
                ?? manifest.GetVideoOnlyStreams().GetWithHighestVideoQuality();
            if (videoOnly == null)
            {
                PlaybackDiagnostics.LogError($"ResolveStreams no video-only stream videoId={videoId}");
                return null;
            }

            var companionAudio = SelectPreferredAudioStream(manifest);
            PlaybackDiagnostics.Log(
                $"ResolveStreams using videoOnly container={videoOnly.Container} companionAudio={(companionAudio != null ? companionAudio.Container.ToString() : "none")}");
            return new RemoteWebVideoStreams
            {
                VideoStream = videoOnly,
                CompanionAudioStream = companionAudio
            };
        }

        private async Task<IStreamInfo?> GetPreferredAudioStreamInfoAsync(string videoId, CancellationToken cancellationToken = default)
        {
            var manifest = await GetStreamManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
            return SelectPreferredAudioStream(manifest);
        }

        /// <summary>
        /// 本地文件仅 .mp4 且在下载记录或调用方中标记为视频时才走视频流；webm 等一律当音频。
        /// </summary>
        private static bool ShouldPlayLocalAsVideo(string filePath, bool markedAsVideo)
        {
            if (!markedAsVideo)
            {
                return false;
            }

            return Path.GetExtension(filePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFileContentType(string filePath, bool isVideo)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".webm")
            {
                return isVideo ? "video/webm" : "audio/webm";
            }

            if (extension == ".mp3")
            {
                return "audio/mpeg";
            }

            return isVideo ? "video/mp4" : "audio/mp4";
        }

        private static string GetStreamContentType(IStreamInfo streamInfo, bool isVideo)
        {
            if (streamInfo.Container == Container.WebM)
            {
                return isVideo ? "video/webm" : "audio/webm";
            }

            return isVideo ? "video/mp4" : "audio/mp4";
        }

        private async Task<string> BuildWebVideoStreamUrlAsync(IStreamInfo videoStreamInfo)
        {
            await EnsureAudioProxyCreatedAsync().ConfigureAwait(false);
            _proxy!.ContentType = GetStreamContentType(videoStreamInfo, true);
            _proxy.CurrentStreamInfo = videoStreamInfo;
            var url = $"{_proxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
            PlaybackDiagnostics.Log($"BuildWebVideoStreamUrl proxy={url} upstream={PlaybackDiagnostics.DescribeUrl(videoStreamInfo.Url)}");
            return url;
        }

        private async Task StartNativeLocalPlaybackAsync(string filePath, PlayingItem video, bool shouldAutoPlay)
        {
            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
            // 不要在此调用 StopAsync：Android Stop 会 StopSelf 销毁 ForegroundService，
            // 与紧随其后的 PlayAsync 竞态会导致本地音频无法起播。PlayAsync 内部会 Stop+换源。

            IsUsingNativePlayback = true;
            IsUsingNativeVideoPlayback = false;
            CurrentStreamUrl = null;
            CurrentTime = 0;
            Duration = 100;
            await _nativeAudio.PlayAsync(filePath, true, video.Title, video.Author, video.DurationSeconds);
            if (!shouldAutoPlay)
            {
                await _nativeAudio.PauseAsync();
            }
        }

        private async Task StopOtherPlaybackPipelineAsync(bool willUseNativePlayback, bool willUseNativeVideoPlayback)
        {
            OnRequestStopWebPlayback?.Invoke();

            if (willUseNativePlayback)
            {
                if (_nativeVideo.IsSupported)
                {
                    await _nativeVideo.StopAsync();
                }
                IsUsingNativeVideoPlayback = false;
                return;
            }

            if (willUseNativeVideoPlayback)
            {
                if (_nativeAudio.IsSupported)
                {
                    await _nativeAudio.StopAsync();
                }
                IsUsingNativePlayback = false;
                return;
            }

            if (_nativeAudio.IsSupported)
            {
                await _nativeAudio.StopAsync();
            }
            if (_nativeVideo.IsSupported)
            {
                await _nativeVideo.StopAsync();
            }
        }

        private string BuildLocalProxyStreamUrl(string localFilePath)
        {
            var fileKey = Uri.EscapeDataString(localFilePath);
            return $"{_fileProxy!.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}&f={fileKey}";
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        private void AddToPlaybackHistory(PlayingItem item, bool isVideo)
        {
            var hasLocalPath = !string.IsNullOrWhiteSpace(item.LocalFilePath);
            if (!hasLocalPath && string.IsNullOrWhiteSpace(item.VideoId))
            {
                return;
            }

            _playbackHistory.RemoveAll(existing =>
                hasLocalPath
                    ? string.Equals(existing.LocalFilePath, item.LocalFilePath, StringComparison.OrdinalIgnoreCase)
                    : existing.VideoId == item.VideoId);

            _playbackHistory.Insert(0, new PlaybackHistoryItem
            {
                VideoId = item.VideoId,
                Title = item.Title,
                Author = item.Author,
                ThumbnailUrl = item.ThumbnailUrl,
                LocalFilePath = item.LocalFilePath,
                IsVideo = isVideo,
                DurationSeconds = item.DurationSeconds,
                PlayedAtUtc = DateTime.UtcNow
            });

            if (_playbackHistory.Count > 50)
            {
                _playbackHistory.RemoveRange(50, _playbackHistory.Count - 50);
            }
        }

        private void OnNativePositionChanged(double currentTime, double duration)
        {
            CurrentTime = currentTime;
            Duration = duration > 0 ? duration : 100;
            OnTimeChanged?.Invoke();
        }

        private void OnNativePlayingStateChanged(bool isPlaying)
        {
            IsPlaying = isPlaying;
            NotifyStateChanged();
        }

        private void OnNativePlaybackEnded()
        {
            _ = Task.Run(OnTrackEndedAsync);
        }

        private void OnNativeVideoPositionChanged(double currentTime, double duration)
        {
            CurrentTime = currentTime;
            Duration = duration > 0 ? duration : 100;
            OnTimeChanged?.Invoke();
        }

        private void OnNativeVideoPlayingStateChanged(bool isPlaying)
        {
            IsPlaying = isPlaying;
            NotifyStateChanged();
        }

        private void OnNativeVideoPlaybackEnded()
        {
            _ = Task.Run(OnTrackEndedAsync);
        }

        private void OnNativeVideoPlaybackStopped()
        {
            if (!IsUsingNativeVideoPlayback)
            {
                return;
            }

            IsUsingNativeVideoPlayback = false;
            IsPlaying = false;
            IsCurrentStreamVideo = false;
            CurrentStreamUrl = null;

            if (CurrentVideo != null)
            {
                CurrentVideo.IsVideo = false;
            }

            NotifyStateChanged();
        }

    }
}
