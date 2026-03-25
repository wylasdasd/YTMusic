using System;
using System.IO;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class AppResetService
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILocalMusicService _localMusicService;
        private readonly UiPreferencesService _uiPreferencesService;
        private readonly MusicPlayerService _musicPlayerService;

        public AppResetService(
            IFavoriteService favoriteService,
            ILocalMusicService localMusicService,
            UiPreferencesService uiPreferencesService,
            MusicPlayerService musicPlayerService)
        {
            _favoriteService = favoriteService;
            _localMusicService = localMusicService;
            _uiPreferencesService = uiPreferencesService;
            _musicPlayerService = musicPlayerService;
        }

        public async Task ResetToDefaultsAsync()
        {
            await _musicPlayerService.ResetStateAsync();
            await _favoriteService.ResetAllAsync();
            await _localMusicService.ResetAllAsync();
            ClearDownloadedMediaDirectory();
            _uiPreferencesService.ResetToDefaults();
            _musicPlayerService.SetUseWebM(_uiPreferencesService.PreferHighQualityAudio);
        }

        private static void ClearDownloadedMediaDirectory()
        {
            var directory = StoragePaths.GetDownloadedMusicDirectory();
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
    }
}
