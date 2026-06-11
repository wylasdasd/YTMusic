using System;
using Microsoft.Maui.Storage;

namespace YTMusic.Services
{
    public enum RemoteVideoStreamQuality
    {
        Lowest = 0,
        Highest = 1
    }

    public class UiPreferencesService
    {
        private const string ShowFavoriteCardImagesKey = "ui.showFavoriteCardImages";
        private const string MediaTitleTwoLinesKey = "ui.mediaTitleTwoLines";
        private const string ThemeIndexKey = "ui.themeIndex";
        private const string PrefetchRemoteVideoKey = "playback.prefetchRemoteVideo";
        private const string RemoteVideoStreamQualityKey = "playback.remoteVideoStreamQuality";

        public bool ShowFavoriteCardImages { get; private set; } = Preferences.Default.Get(ShowFavoriteCardImagesKey, true);
        public bool MediaTitleTwoLines { get; private set; } = LoadMediaTitleTwoLines();
        public int ThemeIndex { get; private set; } = Preferences.Default.Get(ThemeIndexKey, 0);
        public bool PrefetchRemoteVideo { get; private set; } = Preferences.Default.Get(PrefetchRemoteVideoKey, false);
        public RemoteVideoStreamQuality RemoteVideoStreamQuality { get; private set; } = LoadRemoteVideoStreamQuality();

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

        public void SetMediaTitleTwoLines(bool value)
        {
            if (MediaTitleTwoLines == value)
            {
                return;
            }

            MediaTitleTwoLines = value;
            Preferences.Default.Set(MediaTitleTwoLinesKey, value);
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

        public void SetPrefetchRemoteVideo(bool value)
        {
            if (PrefetchRemoteVideo == value)
            {
                return;
            }

            PrefetchRemoteVideo = value;
            Preferences.Default.Set(PrefetchRemoteVideoKey, value);
            OnChange?.Invoke();
        }

        public void SetRemoteVideoStreamQuality(RemoteVideoStreamQuality value)
        {
            if (RemoteVideoStreamQuality == value)
            {
                return;
            }

            RemoteVideoStreamQuality = value;
            Preferences.Default.Set(RemoteVideoStreamQualityKey, (int)value);
            OnChange?.Invoke();
        }

        public void ResetToDefaults()
        {
            ShowFavoriteCardImages = true;
            MediaTitleTwoLines = true;
            ThemeIndex = 0;
            PrefetchRemoteVideo = false;
            RemoteVideoStreamQuality = RemoteVideoStreamQuality.Lowest;

            Preferences.Default.Remove(ShowFavoriteCardImagesKey);
            Preferences.Default.Remove(MediaTitleTwoLinesKey);
            Preferences.Default.Remove(ThemeIndexKey);
            Preferences.Default.Remove(PrefetchRemoteVideoKey);
            Preferences.Default.Remove(RemoteVideoStreamQualityKey);
            Preferences.Default.Remove("ui.scrollMediaTitles");
            Preferences.Default.Remove("ui.preferHighQualityAudio");
            OnChange?.Invoke();
        }

        private static bool LoadMediaTitleTwoLines()
        {
            if (Preferences.Default.ContainsKey(MediaTitleTwoLinesKey))
            {
                return Preferences.Default.Get(MediaTitleTwoLinesKey, true);
            }

            return Preferences.Default.Get("ui.scrollMediaTitles", true);
        }

        private static RemoteVideoStreamQuality LoadRemoteVideoStreamQuality()
        {
            var raw = Preferences.Default.Get(RemoteVideoStreamQualityKey, (int)RemoteVideoStreamQuality.Lowest);
            return Enum.IsDefined(typeof(RemoteVideoStreamQuality), raw)
                ? (RemoteVideoStreamQuality)raw
                : RemoteVideoStreamQuality.Lowest;
        }
    }
}
