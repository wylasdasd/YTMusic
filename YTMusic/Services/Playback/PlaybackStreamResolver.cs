using YoutubeExplode.Videos.Streams;
using YTMusic.BLL.Ports;

namespace YTMusic.Services.Playback;

/// <summary>YouTube 在线流解析（基于 <see cref="IYouTubeApiClient"/>）。</summary>
public sealed class PlaybackStreamResolver
{
    private readonly IYouTubeApiClient _youTubeApiClient;

    public PlaybackStreamResolver(IYouTubeApiClient youTubeApiClient)
    {
        _youTubeApiClient = youTubeApiClient;
    }

    public Task<StreamManifest> GetStreamManifestAsync(string videoId, CancellationToken cancellationToken = default)
        => _youTubeApiClient.GetStreamManifestAsync(videoId);

    public static IStreamInfo? SelectPreferredAudioStream(StreamManifest manifest)
        => manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM)
            .GetWithHighestBitrate() ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

    public async Task<IStreamInfo?> GetPreferredAudioStreamInfoAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var manifest = await GetStreamManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
        return SelectPreferredAudioStream(manifest);
    }

    public async Task<RemoteWebVideoStreams?> ResolveRemoteWebVideoStreamsAsync(
        string videoId,
        CancellationToken cancellationToken = default,
        bool preferLowestVideo = true)
    {
        var manifest = await GetStreamManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
        var muxedCount = manifest.GetMuxedStreams().Count();
        var videoOnlyCount = manifest.GetVideoOnlyStreams().Count();
        var audioOnlyCount = manifest.GetAudioOnlyStreams().Count();
        PlaybackDiagnostics.Log(
            $"ResolveStreams videoId={videoId} muxed={muxedCount} videoOnly={videoOnlyCount} audioOnly={audioOnlyCount} preferLowest={preferLowestVideo}");

        var muxed = manifest.GetMuxedStreams().GetWithHighestVideoQuality();
        if (muxed != null)
        {
            PlaybackDiagnostics.Log($"ResolveStreams using muxed container={muxed.Container}");
            return new RemoteWebVideoStreams { VideoStream = muxed, CompanionAudioStream = null };
        }

        var videoOnly = preferLowestVideo
            ? SelectLowestVideoOnlyStream(manifest)
            : manifest.GetVideoOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality()
                ?? manifest.GetVideoOnlyStreams().GetWithHighestVideoQuality();
        if (videoOnly == null)
        {
            PlaybackDiagnostics.LogError($"ResolveStreams no video-only stream videoId={videoId}");
            return null;
        }

        var companionAudio = SelectPreferredAudioStream(manifest);
        var videoHeight = videoOnly is IVideoStreamInfo videoStream
            ? videoStream.VideoQuality.MaxHeight
            : 0;
        PlaybackDiagnostics.Log(
            $"ResolveStreams using videoOnly container={videoOnly.Container} height={videoHeight} companionAudio={(companionAudio != null ? companionAudio.Container.ToString() : "none")}");
        return new RemoteWebVideoStreams
        {
            VideoStream = videoOnly,
            CompanionAudioStream = companionAudio
        };
    }

    private static IVideoStreamInfo? SelectLowestVideoOnlyStream(StreamManifest manifest)
    {
        var mp4Streams = manifest.GetVideoOnlyStreams()
            .Where(s => s.Container == Container.Mp4)
            .OrderBy(s => s.VideoQuality.MaxHeight)
            .ToList();

        IVideoStreamInfo? selected = mp4Streams.Count > 0
            ? mp4Streams[0]
            : manifest.GetVideoOnlyStreams()
                .OrderBy(s => s.VideoQuality.MaxHeight)
                .FirstOrDefault();

        if (selected == null)
        {
            return null;
        }

        PlaybackDiagnostics.Log(
            $"ResolveStreams lowest video height={selected.VideoQuality.MaxHeight} label={selected.VideoQuality.Label} container={selected.Container}");
        return selected;
    }
}
