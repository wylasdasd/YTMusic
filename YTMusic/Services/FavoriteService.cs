using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Dapper;
using CommonTool.FileHelps;

namespace YTMusic.Services
{
    public class FavoriteFolder
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class FavoriteTrack
    {
        public string VideoId { get; set; } = string.Empty;
        public int FolderId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime AddedDate { get; set; }
        public string? LocalFilePath { get; set; }
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath);
    }

    public interface IFavoriteService
    {
        Task<List<FavoriteFolder>> GetFoldersAsync();
        Task<FavoriteFolder> CreateFolderAsync(string name);
        Task DeleteFolderAsync(int folderId);
        
        Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? isDownloaded = null);
        Task AddToFavoritesAsync(string videoId, string title, string author, string? thumbnailUrl, int folderId = 1, string? localFilePath = null);
        Task RemoveFromFavoritesAsync(string videoId, int folderId = 1);
        Task UpdateLocalFilePathAsync(string videoId, string? localFilePath);
        Task<bool> IsFavoriteAsync(string videoId, int folderId = 1);
        Task<bool> IsFavoriteInAnyFolderAsync(string videoId);
        Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId);
        Task DeleteDownloadedFileAndRecordAsync(string videoId, int folderId = 1);
        Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath);
        Task RemoveTrackByFilePathAsync(string filePath);
    }

    public class FavoriteService : IFavoriteService
    {
        private readonly string _connectionString;

        public FavoriteService()
        {
            SQLitePCL.Batteries_V2.Init();

            string baseDirectory;
            try
            {
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch
            {
                baseDirectory = Environment.CurrentDirectory;
            }

            string dbDir = baseDirectory;
            FileHelp.EnsureDirectoryExists(dbDir);
            string dbPath = Path.Combine(dbDir, "YTMusicFavorites.db3");
            _connectionString = $"Data Source={dbPath}";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            
            string createFoldersSql = @"
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

            string createTracksSql = @"
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
            
            // Migrate old data if present
            try {
                var oldExists = connection.ExecuteScalar<int>("SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Favorites';") > 0;
                if (oldExists) {
                    connection.Execute(@"
                        INSERT OR IGNORE INTO FavoriteTracks (VideoId, FolderId, Title, Author, ThumbnailUrl, AddedDate)
                        SELECT VideoId, 1, Title, Author, ThumbnailUrl, AddedDate FROM Favorites;
                        DROP TABLE Favorites;
                    ");
                }
            } catch {}
        }

        public async Task<List<FavoriteFolder>> GetFoldersAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            return (await connection.QueryAsync<FavoriteFolder>("SELECT * FROM FavoriteFolders ORDER BY IsDefault DESC, Id ASC;")).ToList();
        }

        public async Task<FavoriteFolder> CreateFolderAsync(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "INSERT INTO FavoriteFolders (Name, IsDefault) VALUES (@Name, 0); SELECT last_insert_rowid();";
            int id = await connection.ExecuteScalarAsync<int>(sql, new { Name = name });
            return new FavoriteFolder { Id = id, Name = name, IsDefault = false };
        }

        public async Task DeleteFolderAsync(int folderId)
        {
            if (folderId == 1) throw new InvalidOperationException("Cannot delete default folder.");
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync("DELETE FROM FavoriteTracks WHERE FolderId = @FolderId;", new { FolderId = folderId });
            await connection.ExecuteAsync("DELETE FROM FavoriteFolders WHERE Id = @FolderId;", new { FolderId = folderId });
        }

        public async Task<List<FavoriteTrack>> GetTracksAsync(int? folderId = null, bool? isDownloaded = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            var query = "SELECT * FROM FavoriteTracks";
            var conditions = new List<string>();
            
            if (folderId.HasValue) conditions.Add("FolderId = @FolderId");
            if (isDownloaded.HasValue) 
            {
                if (isDownloaded.Value) conditions.Add("LocalFilePath IS NOT NULL AND LocalFilePath != ''");
                else conditions.Add("(LocalFilePath IS NULL OR LocalFilePath = '')");
            }
            
            if (conditions.Any()) query += " WHERE " + string.Join(" AND ", conditions);
            query += " ORDER BY AddedDate DESC;";

            var tracks = (await connection.QueryAsync<FavoriteTrack>(query, new { FolderId = folderId })).ToList();
            
            if (isDownloaded.HasValue && isDownloaded.Value)
            {
                tracks = tracks.Where(t => t.IsDownloaded).ToList();
            }
            return tracks;
        }

        public async Task AddToFavoritesAsync(string videoId, string title, string author, string? thumbnailUrl, int folderId = 1, string? localFilePath = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT OR REPLACE INTO FavoriteTracks (VideoId, FolderId, Title, Author, ThumbnailUrl, AddedDate, LocalFilePath) 
                VALUES (@VideoId, @FolderId, @Title, @Author, @ThumbnailUrl, @AddedDate, @LocalFilePath);";

            await connection.ExecuteAsync(sql, new 
            { 
                VideoId = videoId, 
                FolderId = folderId,
                Title = title, 
                Author = author, 
                ThumbnailUrl = thumbnailUrl,
                AddedDate = DateTime.UtcNow,
                LocalFilePath = localFilePath
            });
        }

        public async Task RemoveFromFavoritesAsync(string videoId, int folderId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "DELETE FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
            await connection.ExecuteAsync(sql, new { VideoId = videoId, FolderId = folderId });
        }

        public async Task UpdateLocalFilePathAsync(string videoId, string? localFilePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "UPDATE FavoriteTracks SET LocalFilePath = @LocalFilePath WHERE VideoId = @VideoId;";
            await connection.ExecuteAsync(sql, new { VideoId = videoId, LocalFilePath = localFilePath });
        }

        public async Task<bool> IsFavoriteAsync(string videoId, int folderId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT COUNT(1) FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId, FolderId = folderId });
            return count > 0;
        }

        public async Task<bool> IsFavoriteInAnyFolderAsync(string videoId)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT COUNT(1) FROM FavoriteTracks WHERE VideoId = @VideoId;";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId });
            return count > 0;
        }

        public async Task<List<int>> GetFavoriteFolderIdsForVideoAsync(string videoId)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT FolderId FROM FavoriteTracks WHERE VideoId = @VideoId;";
            var ids = await connection.QueryAsync<int>(sql, new { VideoId = videoId });
            return ids.ToList();
        }

        public async Task DeleteDownloadedFileAndRecordAsync(string videoId, int folderId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            string selectSql = "SELECT LocalFilePath FROM FavoriteTracks WHERE VideoId = @VideoId AND FolderId = @FolderId;";
            var path = await connection.ExecuteScalarAsync<string>(selectSql, new { VideoId = videoId, FolderId = folderId });
            
             FileHelp.DeleteIfExists(path);

            await RemoveFromFavoritesAsync(videoId, folderId);
        }

        public async Task<FavoriteTrack?> GetTrackByFilePathAsync(string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "SELECT * FROM FavoriteTracks WHERE LocalFilePath = @LocalFilePath LIMIT 1;";
            return await connection.QueryFirstOrDefaultAsync<FavoriteTrack>(sql, new { LocalFilePath = filePath });
        }

        public async Task RemoveTrackByFilePathAsync(string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "DELETE FROM FavoriteTracks WHERE LocalFilePath = @LocalFilePath;";
            await connection.ExecuteAsync(sql, new { LocalFilePath = filePath });
        }
    }
}