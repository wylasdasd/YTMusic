using System.Net;
using System.Net.Http.Headers;
using YoutubeExplode.Videos.Streams;
using YTMusic.Services;

namespace YTMusic.Infrastructure.Proxies;

/// <summary>
/// 将 YouTube 远程流通过本地 <see cref="HttpListener"/> 暴露为 HTTP URL，供 WebView 绕过 CORS。
/// </summary>
public sealed class LocalAudioProxy : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly int _port;
    private static readonly HttpClient _httpClient = new();

    public string ProxyUrl { get; private set; }
    public IStreamInfo? CurrentStreamInfo { get; set; }
    public string ContentType { get; set; } = "audio/mp4";

    public LocalAudioProxy()
    {
        _cts = new CancellationTokenSource();

        var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        _port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
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
            catch
            {
                // Listener stopped.
            }
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
            var isRange = false;

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

            if (upstreamResponse.Content.Headers.ContentType != null)
            {
                response.ContentType = upstreamResponse.Content.Headers.ContentType.ToString();
            }

            if (upstreamResponse.Content.Headers.ContentLength.HasValue)
            {
                response.ContentLength64 = upstreamResponse.Content.Headers.ContentLength.Value;
            }

            if (upstreamResponse.StatusCode == HttpStatusCode.PartialContent && upstreamResponse.Content.Headers.ContentRange != null)
            {
                response.StatusCode = (int)HttpStatusCode.PartialContent;
                response.AddHeader("Content-Range", upstreamResponse.Content.Headers.ContentRange.ToString());
            }
            else
            {
                response.StatusCode = (int)upstreamResponse.StatusCode;
            }

            await using var youtubeStream = await upstreamResponse.Content.ReadAsStreamAsync(_cts.Token);

            var buffer = new byte[81920];
            int read;
            while ((read = await youtubeStream.ReadAsync(buffer, _cts.Token)) > 0 && !_cts.IsCancellationRequested)
            {
                await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);
            }
        }
        catch (Exception)
        {
            // Client disconnected or stream aborted during seeking/closing.
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch
            {
                // Response already closed.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
    }
}
