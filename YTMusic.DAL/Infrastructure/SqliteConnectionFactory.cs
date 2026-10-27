using System.IO;
using CommonHelp;
using Microsoft.Data.Sqlite;
using YTMusic.BLL.Ports;

namespace YTMusic.DAL.Infrastructure;

internal sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IDatabasePathProvider pathProvider, string databaseFileName)
    {
        SqliteBootstrap.EnsureInitialized();

        var dbDir = pathProvider.GetDatabaseDirectory();
        FileHelp.EnsureDirectoryExists(dbDir + Path.DirectorySeparatorChar);
        var dbPath = Path.Combine(dbDir, databaseFileName);
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
