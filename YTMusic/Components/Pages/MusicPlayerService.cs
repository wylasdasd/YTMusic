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
    }

    public class LocalAudioProxy : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly int _port;
        private readonly YoutubeClient _youtubeClient;
        private static readonly HttpClient _httpClient = new HttpClient();

        public string ProxyUrl { get; private set; }
        public IStreamInfo? CurrentStreamInfo { get; set; }
        public string ContentType { get; set; } = "audio/mp4";

        public LocalAudioProxy(YoutubeClient youtubeClient)
        {
            _youtubeClient = youtubeClient;
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
        private readonly LocalAudioProxy _proxy;
        private readonly LocalFileProxy _fileProxy;
        
        public event Action? OnChange;
        public event Action? OnTimeChanged;
        public event Action? OnRequestReplay;
        public PlayingItem? CurrentVideo { get; private set; }
        public string? CurrentStreamUrl { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLoading { get; private set; }
        public bool UseWebM { get; set; } = false;
        public bool IsCurrentStreamWebM { get; private set; }
        public double CurrentTime { get; private set; } = 0;
        public double Duration { get; private set; } = 100;

        // Playlist State
        public List<PlayingItem> Playlist { get; private set; } = new List<PlayingItem>();
        public int CurrentPlaylistIndex { get; private set; } = -1;
        public string? CurrentPlaylistName { get; private set; }
        public enum PlaybackMode { Sequential, Random, SingleLoop }
        public PlaybackMode CurrentMode { get; private set; } = PlaybackMode.Sequential;
        private List<int> _shuffleIndices = new List<int>();
        private Random _random = new Random();

        public MusicPlayerService()
        {
            _youtubeClient = new YoutubeClient();
            _proxy = new LocalAudioProxy(_youtubeClient);
            _fileProxy = new LocalFileProxy();
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
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url
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
                ThumbnailUrl = video.Thumbnails.FirstOrDefault()?.Url
            });
        }

        public async Task PlayAsync(PlayingItem playingItem)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            await PlayInternalAsync(playingItem);
        }

        public Task PlayLocalFileAsync(string filePath, string title)
        {
            ClearPlaylist();
            CurrentMode = PlaybackMode.SingleLoop;
            IsLoading = true;
            CurrentVideo = new PlayingItem
            {
                VideoId = "local",
                Title = title,
                Author = "Local File",
                ThumbnailUrl = null, // Could be a default local file icon
                LocalFilePath = filePath
            };
            IsPlaying = false;
            NotifyStateChanged();

            try
            {
                IsCurrentStreamWebM = filePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
                _fileProxy.ContentType = IsCurrentStreamWebM ? "audio/webm" : (filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" : "audio/mp4");
                _fileProxy.CurrentFilePath = filePath;
                
                CurrentStreamUrl = $"{_fileProxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
                IsPlaying = true;
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
            
            return Task.CompletedTask;
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

            var indices = Enumerable.Range(0, Playlist.Count).ToList();
            
            if (CurrentPlaylistIndex >= 0 && CurrentPlaylistIndex < Playlist.Count)
            {
                indices.RemoveAt(CurrentPlaylistIndex);
            }

            _shuffleIndices.Add(CurrentPlaylistIndex);
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
                // Re-play current without full reload
                OnRequestReplay?.Invoke();
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
                NotifyStateChanged();

                // 获取流媒体清单
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.VideoId);
                
                IStreamInfo? streamInfo = null;

                if (UseWebM)
                {
                    // 使用 WebM / 最高音质
                    streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                }
                else
                {
                    // 获取兼容性最好的音频流 (优先 MP4/AAC, 其次再选其他最高音质)
                    streamInfo = streamManifest.GetAudioOnlyStreams()
                        .Where(s => s.Container == Container.Mp4)
                        .GetWithHighestBitrate() ?? streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                }

                if (streamInfo != null)
                {
                    IsCurrentStreamWebM = streamInfo.Container.Name.ToLower().Contains("webm");
                    _proxy.ContentType = IsCurrentStreamWebM ? "audio/webm" : "audio/mp4";
                    _proxy.CurrentStreamInfo = streamInfo;
                    
                    // Add a cache buster so the browser doesn't cache the old proxy response
                    CurrentStreamUrl = $"{_proxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
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

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}