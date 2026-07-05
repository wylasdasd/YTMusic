using Dapper;
using Microsoft.Data.Sqlite;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.DAL.Infrastructure;

namespace YTMusic.DAL.Repositories;

public sealed class DownloadedTrackRepository : IDownloadedTrackRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DownloadedTrackRepository(IDatabasePathProvider pathProvider)
    {
        _connectionFactory = new SqliteConnectionFactory(pathProvider, "YTMusicDownloads.db3");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS DownloadedTracks (
                VideoId TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Author TEXT,
                ThumbnailUrl TEXT,
                LocalFilePath TEXT NOT NULL,
                IsVideo INTEGER NOT NULL DEFAULT 0,
                DownloadedDate DATETIME NOT NULL,
                HasUploaded INTEGER NOT NULL DEFAULT 0,
                UploadedDate DATETIME NULL,
                UploadedRemotePath TEXT NULL,
                RemoteSourcePath TEXT NULL
            );";
        connection.Execute(createTableSql);

        EnsureColumn(connection, "IsVideo", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "HasUploaded", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "UploadedDate", "DATETIME NULL");
        EnsureColumn(connection, "UploadedRemotePath", "TEXT NULL");
        EnsureColumn(connection, "RemoteSourcePath", "TEXT NULL");
    }

    private static void EnsureColumn(SqliteConnection connection, string columnName, string columnDefinition)
    {
        SqliteSchemaMigration.EnsureColumn(connection, "DownloadedTracks", columnName, columnDefinition);
    }

    public async Task<IReadOnlyList<DownloadedTrack>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var tracks = await connection.QueryAsync<DownloadedTrack>(
            "SELECT * FROM DownloadedTracks ORDER BY DownloadedDate DESC;");
        return tracks.ToList();
    }

    public async Task<DownloadedTrack?> GetByVideoIdAsync(string videoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DownloadedTrack>(
            "SELECT * FROM DownloadedTracks WHERE VideoId = @VideoId LIMIT 1;",
            new { VideoId = videoId });
    }

    public async Task<DownloadedTrack?> GetByFilePathAsync(string filePath)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DownloadedTrack>(
            "SELECT * FROM DownloadedTracks WHERE LocalFilePath = @LocalFilePath LIMIT 1;",
            new { LocalFilePath = filePath });
    }

    public async Task<DownloadedTrack?> GetByRemoteSourcePathAsync(string remoteSourcePath)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DownloadedTrack>(
            "SELECT * FROM DownloadedTracks WHERE RemoteSourcePath = @RemoteSourcePath LIMIT 1;",
            new { RemoteSourcePath = remoteSourcePath });
    }

    public async Task AddOrReplaceAsync(DownloadedTrack track)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT OR REPLACE INTO DownloadedTracks (VideoId, Title, Author, ThumbnailUrl, LocalFilePath, IsVideo, DownloadedDate, HasUploaded, UploadedDate, UploadedRemotePath, RemoteSourcePath)
            VALUES (@VideoId, @Title, @Author, @ThumbnailUrl, @LocalFilePath, @IsVideo, @DownloadedDate, @HasUploaded, @UploadedDate, @UploadedRemotePath, @RemoteSourcePath);";

        await connection.ExecuteAsync(sql, track);
    }

    public async Task MarkUploadedAsync(string localFilePath, string remotePath, DateTime uploadedDate)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE DownloadedTracks
            SET HasUploaded = 1,
                UploadedDate = @UploadedDate,
                UploadedRemotePath = @UploadedRemotePath
            WHERE LocalFilePath = @LocalFilePath;";

        await connection.ExecuteAsync(sql, new
        {
            LocalFilePath = localFilePath,
            UploadedDate = uploadedDate,
            UploadedRemotePath = remotePath
        });
    }

    public async Task DeleteByVideoIdAsync(string videoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM DownloadedTracks WHERE VideoId = @VideoId;",
            new { VideoId = videoId });
    }

    public async Task ResetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM DownloadedTracks;");
    }
}
