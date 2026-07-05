using Microsoft.Maui.Storage;
using YTMusic.BLL.Ports;

namespace YTMusic.Adapters;

public sealed class MauiPreferencesStore : IPreferencesStore
{
    public string Get(string key, string defaultValue = "") => Preferences.Default.Get(key, defaultValue);

    public void Set(string key, string value) => Preferences.Default.Set(key, value);
}
