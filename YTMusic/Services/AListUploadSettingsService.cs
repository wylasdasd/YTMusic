using System;
using Microsoft.Maui.Storage;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;

namespace YTMusic.Services
{
    public class AListUploadSettingsService : IAListUploadSettingsService
    {
        private const string BaseUrlKey = "alist.upload.baseUrl";
        private const string TokenKey = "alist.upload.token";
        private const string RemoteDirectoryKey = "alist.upload.remoteDirectory";

        public string BaseUrl { get; private set; } = Preferences.Default.Get(BaseUrlKey, string.Empty);
        public string Token { get; private set; } = Preferences.Default.Get(TokenKey, string.Empty);
        public string RemoteDirectory { get; private set; } = AListPathHelper.NormalizeDirectory(Preferences.Default.Get(RemoteDirectoryKey, "/"));

        public event Action? OnChange;

        public void Save(string baseUrl, string token, string remoteDirectory)
        {
            var normalizedBaseUrl = AListPathHelper.NormalizeBaseUrl(baseUrl);
            var normalizedToken = token?.Trim() ?? string.Empty;
            var normalizedDirectory = AListPathHelper.NormalizeDirectory(remoteDirectory);

            var changed =
                !string.Equals(BaseUrl, normalizedBaseUrl, StringComparison.Ordinal) ||
                !string.Equals(Token, normalizedToken, StringComparison.Ordinal) ||
                !string.Equals(RemoteDirectory, normalizedDirectory, StringComparison.Ordinal);

            BaseUrl = normalizedBaseUrl;
            Token = normalizedToken;
            RemoteDirectory = normalizedDirectory;

            Preferences.Default.Set(BaseUrlKey, BaseUrl);
            Preferences.Default.Set(TokenKey, Token);
            Preferences.Default.Set(RemoteDirectoryKey, RemoteDirectory);

            if (changed)
            {
                OnChange?.Invoke();
            }
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(Token) &&
            !string.IsNullOrWhiteSpace(RemoteDirectory);

        public static string NormalizeBaseUrl(string? value) => AListPathHelper.NormalizeBaseUrl(value);

        public static string NormalizeDirectory(string? value) => AListPathHelper.NormalizeDirectory(value);
    }
}
