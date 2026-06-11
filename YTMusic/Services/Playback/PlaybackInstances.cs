using System.Threading.Tasks;

namespace YTMusic.Services.Playback
{
    public sealed class NativeAudioPlaybackInstance : IPlaybackInstance
    {
        public PlaybackKind Kind => PlaybackKind.NativeAudio;

        private IPlaybackHost? _host;

        public async Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options)
        {
            _host = host;
            host.SetActivePlaybackKind(Kind);
            host.ResetPlaybackTiming();
            host.UpdateStreamPresentation(null, source.IsWebM, false);

            var sourcePath = source.IsLocalFile ? source.LocalFilePath! : source.StreamUrl!;
            await host.NativeAudio.PlayAsync(
                sourcePath,
                source.IsLocalFile,
                source.Title,
                source.Artist,
                source.DurationSeconds);

            if (!options.AutoPlay)
            {
                await host.NativeAudio.PauseAsync();
            }
        }

        public async Task DetachAsync(bool preserveNativeAudioBackend)
        {
            if (_host == null || preserveNativeAudioBackend)
            {
                return;
            }

            await _host.NativeAudio.DetachAsync();
            _host = null;
        }

        public Task PlayAsync() => _host!.NativeAudio.ResumeAsync();
        public Task PauseAsync() => _host!.NativeAudio.PauseAsync();
        public Task SeekAsync(double positionSeconds) => _host!.NativeAudio.SeekAsync(positionSeconds);
    }

    public sealed class NativeVideoPlaybackInstance : IPlaybackInstance
    {
        public PlaybackKind Kind => PlaybackKind.NativeVideo;

        private IPlaybackHost? _host;

        public async Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options)
        {
            _host = host;
            host.SetActivePlaybackKind(Kind);
            host.ResetPlaybackTiming();
            host.UpdateStreamPresentation(null, source.IsWebM, true);

            var sourcePath = source.IsLocalFile ? source.LocalFilePath! : source.StreamUrl!;
            await host.NativeVideo.PlayAsync(
                sourcePath,
                source.IsLocalFile,
                source.Title,
                source.Artist,
                source.DurationSeconds);

            if (!options.AutoPlay)
            {
                await host.NativeVideo.PauseAsync();
            }
        }

        public async Task DetachAsync(bool preserveNativeAudioBackend)
        {
            if (_host == null)
            {
                return;
            }

            if (_host.NativeVideo.IsSupported)
            {
                await _host.NativeVideo.StopAsync();
            }

            _host = null;
        }

        public Task PlayAsync() => _host!.NativeVideo.ResumeAsync();
        public Task PauseAsync() => _host!.NativeVideo.PauseAsync();
        public Task SeekAsync(double positionSeconds) => _host!.NativeVideo.SeekAsync(positionSeconds);
    }

    public sealed class WebAudioPlaybackInstance : IPlaybackInstance
    {
        public PlaybackKind Kind => PlaybackKind.WebAudio;

        private IPlaybackHost? _host;

        public async Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options)
        {
            _host = host;
            host.SetActivePlaybackKind(Kind);
            host.ResetPlaybackTiming();
            host.UpdateStreamPresentation(source.StreamUrl, source.IsWebM, false);
            host.RequestWebStateSync();

            if (!options.AutoPlay)
            {
                await host.PauseWebPlaybackAsync();
            }
        }

        public async Task DetachAsync(bool preserveNativeAudioBackend)
        {
            if (_host == null)
            {
                return;
            }

            await _host.StopWebPlaybackAsync();
            _host = null;
        }

        public Task PlayAsync() => _host!.PlayWebPlaybackAsync(videoOnly: false);
        public Task PauseAsync() => _host!.PauseWebPlaybackAsync();
        public Task SeekAsync(double positionSeconds) => _host!.SeekWebPlaybackAsync(positionSeconds);
    }

    public sealed class WebMuxedVideoPlaybackInstance : IPlaybackInstance
    {
        public PlaybackKind Kind => PlaybackKind.WebMuxedVideo;

        private IPlaybackHost? _host;

        public async Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options)
        {
            _host = host;
            host.SetActivePlaybackKind(Kind);
            host.ResetPlaybackTiming();
            host.UpdateStreamPresentation(source.StreamUrl, source.IsWebM, true);
            host.RequestWebStateSync();

            if (!options.AutoPlay)
            {
                await host.PauseWebPlaybackAsync();
            }
        }

        public async Task DetachAsync(bool preserveNativeAudioBackend)
        {
            if (_host == null)
            {
                return;
            }

            await _host.StopWebPlaybackAsync();
            _host = null;
        }

        public Task PlayAsync() => _host!.PlayWebPlaybackAsync(videoOnly: false);
        public Task PauseAsync() => _host!.PauseWebPlaybackAsync();
        public Task SeekAsync(double positionSeconds) => _host!.SeekWebPlaybackAsync(positionSeconds);
    }

    public sealed class HybridPlaybackInstance : IPlaybackInstance
    {
        public PlaybackKind Kind => PlaybackKind.Hybrid;

        private IPlaybackHost? _host;

        public async Task AttachAsync(IPlaybackHost host, PlaybackSource source, PlaybackOptions options)
        {
            _host = host;
            host.SetActivePlaybackKind(Kind);
            host.ResetPlaybackTiming();
            host.UpdateStreamPresentation(source.StreamUrl, source.IsWebM, true);

            await host.NativeAudio.PlayAsync(
                source.CompanionAudioUrl!,
                isLocalFile: false,
                source.Title,
                source.Artist,
                source.DurationSeconds);

            if (!options.AutoPlay)
            {
                await host.NativeAudio.PauseAsync();
            }

            host.RequestWebStateSync();
        }

        public async Task DetachAsync(bool preserveNativeAudioBackend)
        {
            if (_host == null)
            {
                return;
            }

            await _host.StopWebPlaybackAsync();

            if (!preserveNativeAudioBackend)
            {
                await _host.NativeAudio.DetachAsync();
            }

            _host = null;
        }

        public async Task PlayAsync()
        {
            await _host!.NativeAudio.ResumeAsync();
            await _host.PlayWebPlaybackAsync(videoOnly: true);
        }

        public async Task PauseAsync()
        {
            await _host!.NativeAudio.PauseAsync();
            await _host.PauseWebPlaybackAsync();
        }

        public async Task SeekAsync(double positionSeconds)
        {
            await _host!.NativeAudio.SeekAsync(positionSeconds);
            await _host.SeekWebPlaybackAsync(positionSeconds);
        }
    }
}
