using System.Net;
using YTMusic.Services;

namespace YTMusic.Infrastructure.Proxies;

/// <summary>
/// 将本地媒体文件通过 <see cref="HttpListener"/> 暴露为 HTTP URL，供 WebView 播放；URL 可带 <c>&amp;f=</c> 区分文件。
/// </summary>
public sealed class LocalFileProxy : IDisposable
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

            await using var fileStream = new FileStream(CurrentFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

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
                read = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead), _cts.Token);

                if (read == 0)
                {
                    break;
                }

                await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);
                totalRead += read;
            }
        }
        catch (Exception)
        {
            // Client disconnected or stream aborted.
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
