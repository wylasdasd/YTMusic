using YTMusic.Services;

namespace YTMusic.Services.Playback;

/// <summary>
/// Short-lived cache of remote video stream manifests for optional background prefetch.
/// </summary>
public sealed class RemoteVideoPrefetchCache
{
    private readonly object _lock = new();
    private string? _videoId;
    private RemoteWebVideoStreams? _streams;
    private RemoteVideoStreamQuality _quality;
    private DateTime _atUtc;
    private readonly TimeSpan _ttl;

    public RemoteVideoPrefetchCache(TimeSpan ttl)
    {
        _ttl = ttl;
    }

    public void Store(string videoId, RemoteWebVideoStreams? streams, RemoteVideoStreamQuality quality)
    {
        lock (_lock)
        {
            _videoId = videoId;
            _streams = streams;
            _quality = quality;
            _atUtc = DateTime.UtcNow;
        }
    }

    public bool TryGet(
        string videoId,
        RemoteVideoStreamQuality quality,
        out RemoteWebVideoStreams? streams)
    {
        lock (_lock)
        {
            if (_videoId == videoId
                && _streams != null
                && _quality == quality
                && DateTime.UtcNow - _atUtc < _ttl)
            {
                streams = _streams;
                return true;
            }
        }

        streams = null;
        return false;
    }
}
