namespace YTMusic.BLL.Ports;

public interface IPreferencesStore
{
    string Get(string key, string defaultValue = "");

    void Set(string key, string value);
}
