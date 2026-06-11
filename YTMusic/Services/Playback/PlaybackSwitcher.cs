using System.Threading;
using System.Threading.Tasks;
using YTMusic.Services.Abstractions.Playback;

namespace YTMusic.Services.Playback
{
    public sealed class PlaybackSwitcher
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private IPlaybackInstance? _active;

        public IPlaybackInstance? Active => _active;
        public PlaybackKind ActiveKind => _active?.Kind ?? PlaybackKind.None;

        public async Task SwitchAsync(
            IPlaybackInstance next,
            IPlaybackHost host,
            PlaybackSource source,
            PlaybackOptions options)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var previous = _active;
                var preserveNative = previous != null && previous.Kind.SharesNativeAudioBackend(next.Kind);

                if (previous != null && !ReferenceEquals(previous, next))
                {
                    await previous.DetachAsync(preserveNative).ConfigureAwait(false);
                }

                await next.AttachAsync(host, source, options).ConfigureAwait(false);
                _active = next;

                PlaybackDiagnostics.Log(
                    $"PlaybackSwitcher active={next.Kind} preserveNative={preserveNative} url={PlaybackDiagnostics.DescribeUrl(source.StreamUrl)}");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DetachAllAsync(IPlaybackHost host)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_active != null)
                {
                    await _active.DetachAsync(preserveNativeAudioBackend: false).ConfigureAwait(false);
                    _active = null;
                }

                await host.StopWebPlaybackAsync().ConfigureAwait(false);

                if (host.NativeAudio.IsSupported)
                {
                    await host.NativeAudio.StopAsync().ConfigureAwait(false);
                }

                if (host.NativeVideo.IsSupported)
                {
                    await host.NativeVideo.StopAsync().ConfigureAwait(false);
                }

                host.SetActivePlaybackKind(PlaybackKind.None);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
