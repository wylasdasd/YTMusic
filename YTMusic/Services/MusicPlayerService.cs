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
                    response.StatusCode = 404;
                    return;
                }

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
        private readonly UiPreferencesService _uiPreferences;
        private LocalAudioProxy? _proxy;
        private LocalFileProxy? _fileProxy;
        private readonly SemaphoreSlim _fileProxyInitLock = new SemaphoreSlim(1, 1);
        
        public event Action? OnChange;
        public event Action? OnTimeChanged;
        public event Action? OnRequestReplay;
        public event Action? OnRequestPause;
        public event Action? OnRequestPlay;
        public event Action<double>? OnRequestSeek;
        public PlayingItem? CurrentVideo { get; private set; }
        public string? CurrentStreamUrl { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLoading { get; private set; }
        public bool UseNativePlayback => _nativeAudio.IsSupported;
        public bool IsUsingNativePlayback { get; private set; }
        public bool UseNativeVideoPlayback => _nativeVideo.IsSupported && OperatingSystem.IsAndroid();
        public bool IsUsingNativeVideoPlayback { get; private set; }
        public bool UseWebM { get; private set; }
        public bool IsCurrentStreamWebM { get; private set; }
        public bool IsCurrentStreamVideo { get; private set; }
        public double CurrentTime { get; private set; } = 0;
        public double Duration { get; private set; } = 100;

        // Playlist State
        public List<PlayingItem> Playlist { get; private set; } = new List<PlayingItem>();
        public int CurrentPlaylistIndex { get; private set; } = -1;
        public string? CurrentPlaylistName { get; private set; }
        public enum PlaybackMode { Sequential, Random, SingleLoop }
        public PlaybackMode CurrentMode { get; private set; } = PlaybackMode.Sequential;
        private List<int> _shuffleIndices = new List<int>();

        public MusicPlayerService(INativeAudioPlaybackService nativeAudio, INativeVideoPlaybackService nativeVideo, ILocalMusicService localMusicService, UiPreferencesService uiPreferences)
        {
            _nativeAudio = nativeAudio;
            _nativeVideo = nativeVideo;
            _localMusicService = localMusicService;
            _uiPreferences = uiPreferences;
            _youtubeClient = new YoutubeClient();
            UseWebM = _uiPreferences.PreferHighQualityAudio;

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
            }
        }

        public void SetUseWebM(bool value)
        {
            if (UseWebM == value)
            {
                return;
            }

            UseWebM = value;
            _uiPreferences.SetPreferHighQualityAudio(value);
            NotifyStateChanged();
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

        public async Task PlayAsync(VideoSearchResult video)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            await PlayInternalAsync(new PlayingItem
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url,
                DurationSeconds = video.Duration?.TotalSeconds
            });
        }

        public async Task PlayAsync(IVideo video)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            await PlayInternalAsync(new PlayingItem
            {
                VideoId = video.Id.Value,
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url,
                DurationSeconds = video.Duration?.TotalSeconds
            });
        }

        public async Task PlayAsync(PlayingItem playingItem)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            await PlayInternalAsync(playingItem);
        }

        public async Task PlayLocalFileAsync(string filePath, string title, bool? isVideo = null, string? author = null, string? thumbnailUrl = null)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            IsLoading = true;
            CurrentVideo = new PlayingItem
            {
                VideoId = "local",
                Title = title,
                Author = string.IsNullOrWhiteSpace(author) ? "Local File" : author,
                ThumbnailUrl = thumbnailUrl,
                LocalFilePath = filePath,
                IsVideo = isVideo
            };
            IsPlaying = false;
            IsUsingNativePlayback = false;
            IsUsingNativeVideoPlayback = false;
            NotifyStateChanged();

            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                IsCurrentStreamWebM = filePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                IsCurrentStreamVideo = isVideo ?? IsLikelyVideoFile(filePath);
                if (IsCurrentStreamVideo && UseNativeVideoPlayback)
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = true;
                    CurrentStreamUrl = null;
                    CurrentTime = 0;
                    Duration = 100;
                    await _nativeVideo.PlayAsync(filePath, true, title, string.IsNullOrWhiteSpace(author) ? "Local File" : author, null);
                    IsPlaying = true;
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
                    IsPlaying = true;
                }
                else
                {
                    await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                    IsUsingNativePlayback = false;
                    IsUsingNativeVideoPlayback = false;
                    await EnsureFileProxyCreatedAsync();
                    _fileProxy!.ContentType = GetFileContentType(filePath, IsCurrentStreamVideo);
                    _fileProxy.CurrentFilePath = filePath;
                    CurrentStreamUrl = $"{_fileProxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                    IsPlaying = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing local file: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                NotifyStateChanged();
            }
        }

        public async Task<bool> CanPlayCurrentAsVideoAsync()
        {
            var current = CurrentVideo;
            if (current == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(current.LocalFilePath))
            {
                var localTrack = await _localMusicService.GetDownloadedTrackByFilePathAsync(current.LocalFilePath);
                if (localTrack?.IsVideo == true)
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(current.VideoId);
            if (downloaded?.IsVideo == true)
            {
                return true;
            }

            var muxedStreamInfo = await GetPreferredMuxedVideoStreamInfoAsync(current.VideoId);
            return muxedStreamInfo != null;
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
                if (localTrack?.IsVideo == true && !string.IsNullOrWhiteSpace(localTrack.LocalFilePath))
                {
                    await PlayLocalFileAsync(localTrack.LocalFilePath, current.Title, true, current.Author, current.ThumbnailUrl);
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(current.VideoId) || current.VideoId == "local")
            {
                return false;
            }

            var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(current.VideoId);
            if (downloaded?.IsVideo == true && !string.IsNullOrWhiteSpace(downloaded.LocalFilePath))
            {
                await PlayLocalFileAsync(downloaded.LocalFilePath, current.Title, true, current.Author, current.ThumbnailUrl);
                return true;
            }

            await PlayInternalAsync(new PlayingItem
            {
                VideoId = current.VideoId,
                Title = current.Title,
                Author = current.Author,
                ThumbnailUrl = current.ThumbnailUrl,
                DurationSeconds = current.DurationSeconds,
                IsVideo = true
            });

            return IsCurrentStreamVideo;
        }

        public void ClearPlaylist()
        {
            Playlist.Clear();
            _shuffleIndices.Clear();
            CurrentPlaylistIndex = -1;
            CurrentPlaylistName = null;
            NotifyStateChanged();
        }

        public async Task PlayPlaylistAsync(List<PlayingItem> items, int startIndex, string playlistName, PlaybackMode mode = PlaybackMode.Sequential)
        {
            if (items == null || items.Count == 0 || startIndex < 0 || startIndex >= items.Count)
                return;

            Playlist = items;
            CurrentPlaylistName = playlistName;
            CurrentPlaylistIndex = startIndex;
            CurrentMode = mode;
            GenerateShuffleIndices();

            await PlayInternalAsync(Playlist[CurrentPlaylistIndex]);
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
            if (Playlist.Count == 0) return;

            int nextIndex = GetNextIndex();
            
            if (nextIndex != -1)
            {
                CurrentPlaylistIndex = nextIndex;
                await PlayInternalAsync(Playlist[CurrentPlaylistIndex]);
            }
        }

        public async Task PlayPreviousAsync()
        {
            if (Playlist.Count == 0) return;

            int previousIndex = GetPreviousIndex();

            if (previousIndex != -1)
            {
                CurrentPlaylistIndex = previousIndex;
                await PlayInternalAsync(Playlist[CurrentPlaylistIndex]);
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

        private async Task PlayInternalAsync(PlayingItem video)
        {
            try
            {
                IsLoading = true;
                CurrentVideo = video;
                IsPlaying = false; // Reset playing state while loading
                IsUsingNativePlayback = false;
                IsUsingNativeVideoPlayback = false;
                NotifyStateChanged();

                if (!string.IsNullOrEmpty(video.LocalFilePath))
                {
                    if (!File.Exists(video.LocalFilePath))
                    {
                        return;
                    }

                    IsCurrentStreamWebM = video.LocalFilePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                    IsCurrentStreamVideo = video.IsVideo ?? IsLikelyVideoFile(video.LocalFilePath);

                    if (IsCurrentStreamVideo && UseNativeVideoPlayback)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = true;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeVideo.PlayAsync(video.LocalFilePath, true, video.Title, video.Author, video.DurationSeconds);
                        IsPlaying = true;
                    }
                    else if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = true;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeAudio.PlayAsync(video.LocalFilePath, true, video.Title, video.Author, video.DurationSeconds);
                        IsPlaying = true;
                    }
                    else
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        await EnsureFileProxyCreatedAsync();
                        _fileProxy!.ContentType = GetFileContentType(video.LocalFilePath, IsCurrentStreamVideo);
                        _fileProxy.CurrentFilePath = video.LocalFilePath;
                        CurrentStreamUrl = $"{_fileProxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                        IsPlaying = true;
                    }
                    return;
                }

                // Prefer downloaded local files when we have a record for this video id.
                var downloaded = await _localMusicService.GetDownloadedTrackByVideoIdAsync(video.VideoId);
                if (downloaded != null && !string.IsNullOrWhiteSpace(downloaded.LocalFilePath))
                {
                    video.LocalFilePath = downloaded.LocalFilePath;
                    video.IsVideo = downloaded.IsVideo;
                    IsCurrentStreamWebM = video.LocalFilePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                    IsCurrentStreamVideo = downloaded.IsVideo;

                    if (_nativeAudio.IsSupported && !IsCurrentStreamVideo)
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: true, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = true;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = null;
                        CurrentTime = 0;
                        Duration = 100;
                        await _nativeAudio.PlayAsync(video.LocalFilePath, true, video.Title, video.Author, video.DurationSeconds);
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
                    }
                    else
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        await EnsureFileProxyCreatedAsync();
                        _fileProxy!.ContentType = GetFileContentType(video.LocalFilePath, IsCurrentStreamVideo);
                        _fileProxy.CurrentFilePath = video.LocalFilePath;
                        CurrentStreamUrl = $"{_fileProxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                    }

                    IsPlaying = true;
                    return;
                }

                if (video.IsVideo == true)
                {
                    var muxedStreamInfo = await GetPreferredMuxedVideoStreamInfoAsync(video.VideoId);

                    if (muxedStreamInfo != null)
                    {
                        IsCurrentStreamWebM = muxedStreamInfo.Container == Container.WebM;
                        IsCurrentStreamVideo = true;

                        if (UseNativeVideoPlayback)
                        {
                            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: true);
                            IsUsingNativePlayback = false;
                            IsUsingNativeVideoPlayback = true;
                            CurrentStreamUrl = null;
                            CurrentTime = 0;
                            Duration = 100;
                            await _nativeVideo.PlayAsync(muxedStreamInfo.Url, false, video.Title, video.Author, video.DurationSeconds);
                        }
                        else if (OperatingSystem.IsAndroid())
                        {
                            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                            IsUsingNativePlayback = false;
                            IsUsingNativeVideoPlayback = false;
                            CurrentStreamUrl = muxedStreamInfo.Url;
                        }
                        else
                        {
                            await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                            IsUsingNativePlayback = false;
                            IsUsingNativeVideoPlayback = false;
                            EnsureProxiesCreated();
                            _proxy!.ContentType = GetStreamContentType(muxedStreamInfo, true);
                            _proxy.CurrentStreamInfo = muxedStreamInfo;
                            CurrentStreamUrl = $"{_proxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                        }

                        IsPlaying = true;
                        return;
                    }
                }

                var streamInfo = await GetPreferredAudioStreamInfoAsync(video.VideoId, UseWebM);

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
                    }
                    else if (OperatingSystem.IsAndroid())
                    {
                        await StopOtherPlaybackPipelineAsync(willUseNativePlayback: false, willUseNativeVideoPlayback: false);
                        IsUsingNativePlayback = false;
                        IsUsingNativeVideoPlayback = false;
                        CurrentStreamUrl = streamInfo.Url;
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
                    }

                    IsPlaying = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing video: {ex.Message}");
                // 这里可以添加错误处理逻辑，比如弹窗提示
            }
            finally
            {
                IsLoading = false;
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
                SetPlayingState(false);
            }
        }

        public async Task ResumeAsync()
        {
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
                SetPlayingState(true);
            }
        }

        public async Task SeekAsync(double positionSeconds)
        {
            if (IsUsingNativePlayback)
            {
                await _nativeAudio.SeekAsync(positionSeconds);
            }
            else if (IsUsingNativeVideoPlayback)
            {
                await _nativeVideo.SeekAsync(positionSeconds);
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

        private async Task<IStreamInfo?> GetPreferredAudioStreamInfoAsync(string videoId, bool useWebM)
        {
            if (OperatingSystem.IsAndroid())
            {
                // YoutubeExplode internals may hit synchronous network APIs on Android.
                // Force manifest parsing off the UI thread to avoid NetworkOnMainThreadException.
                return await Task.Run(async () =>
                {
                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    if (useWebM)
                    {
                        return streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    }

                    return streamManifest.GetAudioOnlyStreams()
                        .Where(s => s.Container == Container.Mp4)
                        .GetWithHighestBitrate() ?? streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                });
            }

            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            if (useWebM)
            {
                return manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            }

            return manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestBitrate() ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        }

        private async Task<IStreamInfo?> GetPreferredMuxedVideoStreamInfoAsync(string videoId)
        {
            if (OperatingSystem.IsAndroid())
            {
                return await Task.Run(async () =>
                {
                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    return streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                });
            }

            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            return manifest.GetMuxedStreams().GetWithHighestVideoQuality();
        }

        private static bool IsLikelyVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".mp4" || extension == ".webm" || extension == ".mkv" || extension == ".mov" || extension == ".m4v" || extension == ".avi";
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

        private async Task StopOtherPlaybackPipelineAsync(bool willUseNativePlayback, bool willUseNativeVideoPlayback)
        {
            if (willUseNativePlayback)
            {
                OnRequestPause?.Invoke();
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
                OnRequestPause?.Invoke();
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

        private void NotifyStateChanged() => OnChange?.Invoke();

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

    }
}
