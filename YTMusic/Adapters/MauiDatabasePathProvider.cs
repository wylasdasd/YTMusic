using YTMusic.BLL.Ports;

namespace YTMusic.Adapters;

public sealed class MauiDatabasePathProvider : IDatabasePathProvider
{
    public string GetDatabaseDirectory()
    {
        try
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
    }
}
