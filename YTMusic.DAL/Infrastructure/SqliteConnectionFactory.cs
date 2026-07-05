using System.IO;
using CommonTool.FileHelps;
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
        FileHelp.EnsureDirectoryExists(dbDir);
        var dbPath = Path.Combine(dbDir, databaseFileName);
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
