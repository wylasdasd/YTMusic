using YoutubeExplode.Videos.Streams;

namespace YTMusic.BLL.Infrastructure.YouTube;

/// <summary>
/// 与 YoutubeExplode Readme 一致：优先 audio-only（WebM 优先最高码率）。
/// </summary>
public static class YouTubeStreamSelection
{
    public static IStreamInfo? SelectPreferredAudioStream(StreamManifest manifest)
        => manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM)
            .GetWithHighestBitrate()
            ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
}
