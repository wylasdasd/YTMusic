using Dapper;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.DAL.Infrastructure;

namespace YTMusic.DAL.Repositories;

public sealed class FavoriteRepository : IFavoriteRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public FavoriteRepository(IDatabasePathProvider pathProvider)
    {
        _connectionFactory = new SqliteConnectionFactory(pathProvider, "YTMusicFavorites.db3");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string createFoldersSql = @"
            CREATE TABLE IF NOT EXISTS FavoriteFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                IsDefault INTEGER NOT NULL DEFAULT 0
            );";
        connection.Execute(createFoldersSql);

        var defaultFolder = connection.QueryFirstOrDefault<int>("SELECT COUNT(1) FROM FavoriteFolders WHERE Id = 1;");
        if (defaultFolder == 0)
        {
            connection.Execute("INSERT INTO FavoriteFolders (Id, Name, IsDefault) VALUES (1, '默认收藏夹', 1);");
        }

        const string createTracksSql = @"
            CREATE TABLE IF NOT EXISTS FavoriteTracks (
                VideoId TEXT NOT NULL,
                FolderId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Author TEXT,
                ThumbnailUrl TEXT,
                AddedDate DATETIME NOT NULL,
                LocalFilePath TEXT,
                PRIMARY KEY (VideoId, FolderId),
                FOREIGN KEY (FolderId) REFERENCES FavoriteFolders(Id) ON DELETE CASCADE
            );";
        connection.Execute(createTracksSql);

        var oldExists = connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Favorites';") > 0;
        if (oldExists)
        {
            connection.Execute(@"
                INSERT OR IGNORE INTO FavoriteTracks (VideoId, FolderId, Title, Author, ThumbnailUrl, AddedDate)
                SELECT VideoId, 1, Title, Author, ThumbnailUrl, AddedDate FROM Favorites;
                DROP TABLE Favorites;
            ");
        }
    }

    public async Task<List<FavoriteFolder>> GetFoldersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<FavoriteFolder>(
            "SELECT * FROM FavoriteFolders ORDER BY IsDefault DESC, Id ASC;");
        return rows.ToList();
    }

    public async Task<FavoriteFolder> CreateFolderAsync(string name)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "INSERT INTO FavoriteFolders (Name, IsDefault) VALUES (@Name, 0); SELECT last_insert_rowid();";
        var id = await connection.ExecuteScalarAsync<int>(sql, new { Name = name });
        return new FavoriteFolder { Id = id, Name = name, IsDefault = false };
    }

    public async Task DeleteFolderAsync(int folderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM FavoriteTracks WHERE FolderId = @FolderId;", new { FolderId = folderId });
        await connection.ExecuteAsync("DELETE FROM FavoriteFolders WHERE Id = @FolderId;", new { FolderId = folderId });
    }

    public async Task ResetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM FavoriteTracks;");
        await connection.ExecuteAsync("DELETE FROM FavoriteFolders WHERE Id != 1;");
        await connection.ExecuteAsync("INSERT OR IGNORE INTO FavoriteFolders (Id, Name, IsDefault) VALUES (1, '默认收藏夹', 1);");
        await connection.ExecuteAsync("UPDATE FavoriteFolders SET Name = '默认收藏夹', IsDefault = 1 WHERE Id = 1;");
    }

    public async Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? hasLocalFilePath = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = "SELECT * FROM FavoriteTracks";
        var conditions = new List<string>();

        if (folderId.HasValue)
        {
            conditions.Add("FolderId = @FolderId");
        }

        if (hasLocalFilePath.HasValue)
        {
            if (hasLocalFilePath.Value)
            {
                conditions.Add("LocalFilePath IS NOT NULL AND LocalFilePath != ''");
            }
            else
            {
                conditions.Add("(LocalFilePath IS NULL OR LocalFilePath = '')");
            }
        }

        if (conditions.Count > 0)
        {
            query += " WHERE " + string.Join(" AND ", conditions);
        }

        query += " ORDER BY AddedDate DESC;";

        var tracks = await connection.QueryAsync<FavoriteTrack>(query, new { FolderId = folderId });
        return tracks.ToList();
    }

    public async Task<HashSet<string>> GetFavoritedVideoIdsAsync(IEnumerable<string> videoIds)
    {
        var ids = videoIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new HashSet<string>();
        }

        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT DISTINCT VideoId FROM FavoriteTracks WHERE VideoId IN @VideoIds;";
        var rows = await connection.QueryAsync<string>(sql, new { VideoIds = ids });
        return rows.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet();
    }

    public async Task<HashSet<string>> GetFavoritedFilePathsAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .ToArray();

        if (paths.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT DISTINCT LocalFilePath
            FROM FavoriteTracks
            WHERE LocalFilePath IS NOT NULL
              AND LocalFilePath != ''
              AND LocalFilePath IN @FilePaths;";
        var rows = await connection.QueryAsync<string>(sql, new { FilePaths = paths });
        return rows.Where(path => !string.IsNullOrWhiteSpace(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddTrackAsync(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl,
        int folderId,
        string? localFilePath,
        DateTime addedDate)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT OR REPLACE INTO FavoriteTracks (VideoId, FolderId, Title, Author, ThumbnailUrl, AddedDate, LocalFilePath)
            VALUES (@VideoId, @FolderId, @Title, @Author, @ThumbnailUrl, @AddedDate, @LocalFilePath);";

        await connection.ExecuteAsync(sql, new
        {
            VideoId = videoId,
            FolderId = folderId,
            Title = title,
            Author = author,
            ThumbnailUrl = thumbnailUrl,
            AddedDate = addedDate,
            LocalFilePath = localFilePath
        });
    }

    public async Task RemoveTrackAsync(string videoId, int folderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
        await connection.ExecuteAsync(sql, new { VideoId = videoId, FolderId = folderId });
    }

    public async Task UpdateLocalFilePathAsync(string videoId, string? localFilePath)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE FavoriteTracks SET LocalFilePath = @LocalFilePath WHERE VideoId = @VideoId;";
        await connection.ExecuteAsync(sql, new { VideoId = videoId, LocalFilePath = localFilePath });
    }

    public async Task<bool> IsFavoriteAsync(string videoId, int folderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId, FolderId = folderId });
        return count > 0;
    }

    public async Task<bool> IsFavoriteInAnyFolderAsync(string videoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM FavoriteTracks WHERE VideoId = @VideoId;";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId });
        return count > 0;
    }

    public async Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT FolderId FROM FavoriteTracks WHERE VideoId = @VideoId;";
        var ids = await connection.QueryAsync<int>(sql, new { VideoId = videoId });
        return ids.ToList();
    }

    public async Task<string?> GetLocalFilePathAsync(string videoId, int folderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT LocalFilePath FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
        return await connection.ExecuteScalarAsync<string?>(sql, new { VideoId = videoId, FolderId = folderId });
    }

    public async Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM FavoriteTracks WHERE LocalFilePath = @LocalFilePath LIMIT 1;";
        return await connection.QueryFirstOrDefaultAsync<FavoriteTrack>(sql, new { LocalFilePath = filePath });
    }

    public async Task RemoveTrackByFilePathAsync(string filePath)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM FavoriteTracks WHERE LocalFilePath = @LocalFilePath;";
        await connection.ExecuteAsync(sql, new { LocalFilePath = filePath });
    }
}
