using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Services;

public sealed class AListUploadSettingsService : IAListUploadSettingsService
{
    private const string BaseUrlKey = AppGlobal.AList.PreferenceKeys.BaseUrl;
    private const string TokenKey = AppGlobal.AList.PreferenceKeys.Token;
    private const string RemoteDirectoryKey = AppGlobal.AList.PreferenceKeys.RemoteDirectory;

    public AListUploadSettingsService(IPreferencesStore preferencesStore)
    {
        BaseUrl = preferencesStore.Get(BaseUrlKey, string.Empty);
        Token = preferencesStore.Get(TokenKey, string.Empty);
        RemoteDirectory = AListPathHelper.NormalizeDirectory(preferencesStore.Get(RemoteDirectoryKey, "/"));
        _preferencesStore = preferencesStore;
    }

    private readonly IPreferencesStore _preferencesStore;

    public string BaseUrl { get; private set; }

    public string Token { get; private set; }

    public string RemoteDirectory { get; private set; }

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

        _preferencesStore.Set(BaseUrlKey, BaseUrl);
        _preferencesStore.Set(TokenKey, Token);
        _preferencesStore.Set(RemoteDirectoryKey, RemoteDirectory);

        if (changed)
        {
            OnChange?.Invoke();
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Token) &&
        !string.IsNullOrWhiteSpace(RemoteDirectory);
}
