using System.Net;

namespace YTMusic.BLL.Infrastructure.AList;

/// <summary>带进度上报的 <see cref="HttpContent"/>，用于 AList 流式上传。</summary>
internal sealed class ProgressStreamContent : HttpContent
{
    private readonly Stream _sourceStream;
    private readonly IProgress<double>? _progress;
    private readonly int _bufferSize;
    private readonly bool _disposeSource;
    private readonly long _startPosition;
    private readonly long _contentLength;

    public ProgressStreamContent(Stream sourceStream, IProgress<double>? progress, bool disposeSource = false, int bufferSize = 81_920)
    {
        _sourceStream = sourceStream;
        _progress = progress;
        _disposeSource = disposeSource;
        _bufferSize = bufferSize;

        if (_sourceStream.CanSeek)
        {
            _startPosition = _sourceStream.Position;
            _contentLength = _sourceStream.Length - _startPosition;
            Headers.ContentLength = _contentLength;
        }
        else
        {
            _startPosition = 0;
            _contentLength = -1;
        }
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        if (_sourceStream.CanSeek)
        {
            _sourceStream.Position = _startPosition;
        }

        var buffer = new byte[_bufferSize];
        long totalSent = 0;

        while (_contentLength < 0 || totalSent < _contentLength)
        {
            var toRead = _contentLength < 0
                ? buffer.Length
                : (int)Math.Min(buffer.Length, _contentLength - totalSent);
            if (toRead <= 0)
            {
                break;
            }

            var read = await _sourceStream.ReadAsync(buffer.AsMemory(0, toRead), CancellationToken.None);
            if (read == 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None);
            totalSent += read;

            if (_progress != null && _contentLength > 0)
            {
                _progress.Report((double)totalSent / _contentLength);
            }
        }

        if (_contentLength > 0 && totalSent != _contentLength)
        {
            throw new InvalidOperationException(
                $"Upload stream ended early. Expected {_contentLength} bytes, sent {totalSent} bytes.");
        }

        // 不在此处 Report(1.0)：还需等待 HTTP 响应与远端校验，由 UploadFileToPathAsync 在确认成功后上报。
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_contentLength >= 0)
        {
            length = _contentLength;
            return true;
        }

        length = -1;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _disposeSource)
        {
            _sourceStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
