using System;
using Microsoft.Maui.Storage;

namespace YTMusic.Services
{
    public class AListUploadSettingsService
    {
        private const string BaseUrlKey = "alist.upload.baseUrl";
        private const string TokenKey = "alist.upload.token";
        private const string RemoteDirectoryKey = "alist.upload.remoteDirectory";

        public string BaseUrl { get; private set; } = Preferences.Default.Get(BaseUrlKey, string.Empty);
        public string Token { get; private set; } = Preferences.Default.Get(TokenKey, string.Empty);
        public string RemoteDirectory { get; private set; } = NormalizeDirectory(Preferences.Default.Get(RemoteDirectoryKey, "/"));

        public event Action? OnChange;

        public void Save(string baseUrl, string token, string remoteDirectory)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            var normalizedToken = token?.Trim() ?? string.Empty;
            var normalizedDirectory = NormalizeDirectory(remoteDirectory);

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

        public static string NormalizeBaseUrl(string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            return trimmed.TrimEnd('/');
        }

        public static string NormalizeDirectory(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "/";
            }

            if (!trimmed.StartsWith('/'))
            {
                trimmed = "/" + trimmed;
            }

            while (trimmed.Contains("//", StringComparison.Ordinal))
            {
                trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
            }

            if (trimmed.Length > 1)
            {
                trimmed = trimmed.TrimEnd('/');
            }

            return trimmed;
        }
    }
}
