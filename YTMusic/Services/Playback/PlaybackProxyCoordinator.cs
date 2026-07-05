using CommonTool.TimeHelps;
using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Ports;
using YTMusic.Infrastructure.Proxies;

namespace YTMusic.Services.Playback;

/// <summary>管理 Web 播放所需的本地 HTTP 代理生命周期。</summary>
public sealed class PlaybackProxyCoordinator
{
    private LocalAudioProxy? _audioProxy;
    private LocalFileProxy? _fileProxy;
    private readonly SemaphoreSlim _audioProxyInitLock = new(1, 1);
    private readonly SemaphoreSlim _fileProxyInitLock = new(1, 1);

    public PlaybackProxyCoordinator()
    {
        if (!OperatingSystem.IsAndroid())
        {
            _audioProxy = new LocalAudioProxy();
            _fileProxy = new LocalFileProxy();
        }
    }

    public async Task EnsureAudioProxyCreatedAsync()
    {
        if (_audioProxy != null)
        {
            return;
        }

        await _audioProxyInitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_audioProxy != null)
            {
                return;
            }

            _audioProxy = OperatingSystem.IsAndroid()
                ? await Task.Run(() => new LocalAudioProxy()).ConfigureAwait(false)
                : new LocalAudioProxy();

            PlaybackDiagnostics.Log($"Audio proxy ready url={_audioProxy.ProxyUrl}");
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

    public async Task EnsureFileProxyCreatedAsync()
    {
        if (_fileProxy != null)
        {
            return;
        }

        await _fileProxyInitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_fileProxy != null)
            {
                return;
            }

            _fileProxy = OperatingSystem.IsAndroid()
                ? await Task.Run(() => new LocalFileProxy()).ConfigureAwait(false)
                : new LocalFileProxy();
        }
        finally
        {
            _fileProxyInitLock.Release();
        }
    }

    public void EnsureProxiesCreated()
    {
        _audioProxy ??= new LocalAudioProxy();
        _fileProxy ??= new LocalFileProxy();
    }

    public void ConfigureFileProxy(string filePath, bool isVideo)
    {
        EnsureProxiesCreated();
        _fileProxy!.ContentType = GetFileContentType(filePath, isVideo);
        _fileProxy.CurrentFilePath = filePath;
    }

    public void ConfigureAudioProxy(IStreamInfo streamInfo, bool isVideo)
    {
        EnsureProxiesCreated();
        _audioProxy!.ContentType = GetStreamContentType(streamInfo, isVideo);
        _audioProxy.CurrentStreamInfo = streamInfo;
    }

    public string BuildLocalProxyStreamUrl(string localFilePath)
    {
        EnsureProxiesCreated();
        var fileKey = Uri.EscapeDataString(localFilePath);
        return $"{_fileProxy!.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}&f={fileKey}";
    }

    public async Task<string> BuildWebVideoStreamUrlAsync(IStreamInfo videoStreamInfo)
    {
        await EnsureAudioProxyCreatedAsync().ConfigureAwait(false);
        _audioProxy!.ContentType = GetStreamContentType(videoStreamInfo, true);
        _audioProxy.CurrentStreamInfo = videoStreamInfo;
        var url = $"{_audioProxy.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
        PlaybackDiagnostics.Log($"BuildWebVideoStreamUrl proxy={url} upstream={PlaybackDiagnostics.DescribeUrl(videoStreamInfo.Url)}");
        return url;
    }

    public string BuildAudioProxyStreamUrl(IStreamInfo streamInfo, bool isVideo)
    {
        ConfigureAudioProxy(streamInfo, isVideo);
        return $"{_audioProxy!.ProxyUrl}?t={UnixHelp.GetUtcNowUnixTimeMilliseconds()}";
    }

    public static string GetFileContentType(string filePath, bool isVideo)
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

    public static string GetStreamContentType(IStreamInfo streamInfo, bool isVideo)
    {
        if (streamInfo.Container == Container.WebM)
        {
            return isVideo ? "video/webm" : "audio/webm";
        }

        return isVideo ? "video/mp4" : "audio/mp4";
    }
}
