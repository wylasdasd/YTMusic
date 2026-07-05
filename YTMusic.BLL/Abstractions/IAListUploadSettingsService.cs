namespace YTMusic.BLL.Abstractions;

public interface IAListUploadSettingsService
{
    string BaseUrl { get; }
    string Token { get; }
    string RemoteDirectory { get; }
    bool IsConfigured { get; }
    event Action? OnChange;
    void Save(string baseUrl, string token, string remoteDirectory);
}
