namespace YTMusic.DAL.Infrastructure;

internal static class SqliteBootstrap
{
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        SQLitePCL.Batteries_V2.Init();
        _initialized = true;
    }
}
