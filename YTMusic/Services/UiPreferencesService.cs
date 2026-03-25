using System;
using Microsoft.Maui.Storage;

namespace YTMusic.Services
{
    public class UiPreferencesService
    {
        private const string ShowFavoriteCardImagesKey = "ui.showFavoriteCardImages";
        private const string ThemeIndexKey = "ui.themeIndex";
        private const string PreferHighQualityAudioKey = "ui.preferHighQualityAudio";

        public bool ShowFavoriteCardImages { get; private set; } = Preferences.Default.Get(ShowFavoriteCardImagesKey, true);
        public int ThemeIndex { get; private set; } = Preferences.Default.Get(ThemeIndexKey, 0);
        public bool PreferHighQualityAudio { get; private set; } = Preferences.Default.Get(PreferHighQualityAudioKey, true);

        public event Action? OnChange;

        public void SetShowFavoriteCardImages(bool value)
        {
            if (ShowFavoriteCardImages == value)
            {
                return;
            }

            ShowFavoriteCardImages = value;
            Preferences.Default.Set(ShowFavoriteCardImagesKey, value);
            OnChange?.Invoke();
        }

        public void SetThemeIndex(int value)
        {
            if (ThemeIndex == value)
            {
                return;
            }

            ThemeIndex = value;
            Preferences.Default.Set(ThemeIndexKey, value);
            OnChange?.Invoke();
        }

        public void SetPreferHighQualityAudio(bool value)
        {
            if (PreferHighQualityAudio == value)
            {
                return;
            }

            PreferHighQualityAudio = value;
            Preferences.Default.Set(PreferHighQualityAudioKey, value);
            OnChange?.Invoke();
        }
    }
}
