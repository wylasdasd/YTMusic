using Dapper;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.BLL.Models;
using YTMusic.BLL.Ports;
using YTMusic.DAL.Infrastructure;

namespace YTMusic.DAL.Repositories;

public sealed class PlaybackHistoryRepository : IPlaybackHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PlaybackHistoryRepository(IDatabasePathProvider pathProvider)
    {
        _connectionFactory = new SqliteConnectionFactory(pathProvider, AppGlobal.Database.PlaybackHistoryFileName);
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            CREATE TABLE IF NOT EXISTS PlaybackHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VideoId TEXT NOT NULL DEFAULT '',
                Title TEXT NOT NULL,
                Author TEXT NOT NULL DEFAULT '',
                ThumbnailUrl TEXT,
                LocalFilePath TEXT,
                IsVideo INTEGER NOT NULL DEFAULT 0,
                DurationSeconds REAL,
                PlayedAtUtc TEXT NOT NULL
            );";
        connection.Execute(sql);
    }

    public async Task<IReadOnlyList<PlaybackHistoryRecord>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PlaybackHistoryRecord>(@"
            SELECT Id, VideoId, Title, Author, ThumbnailUrl, LocalFilePath,
                   IsVideo, DurationSeconds, PlayedAtUtc
            FROM PlaybackHistory
            ORDER BY PlayedAtUtc DESC
            LIMIT @Limit;",
            new { Limit = AppGlobal.Transfers.MaxPlaybackHistoryItems });
        return rows.ToList();
    }

    public async Task RecordPlayAsync(PlaybackHistoryRecord record)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (!string.IsNullOrWhiteSpace(record.LocalFilePath))
        {
            await connection.ExecuteAsync(
                "DELETE FROM PlaybackHistory WHERE LocalFilePath = @LocalFilePath COLLATE NOCASE;",
                new { record.LocalFilePath });
        }
        else if (!string.IsNullOrWhiteSpace(record.VideoId))
        {
            await connection.ExecuteAsync(
                "DELETE FROM PlaybackHistory WHERE VideoId = @VideoId AND (LocalFilePath IS NULL OR LocalFilePath = '');",
                new { record.VideoId });
        }

        await connection.ExecuteAsync(@"
            INSERT INTO PlaybackHistory (VideoId, Title, Author, ThumbnailUrl, LocalFilePath, IsVideo, DurationSeconds, PlayedAtUtc)
            VALUES (@VideoId, @Title, @Author, @ThumbnailUrl, @LocalFilePath, @IsVideo, @DurationSeconds, @PlayedAtUtc);",
            new
            {
                record.VideoId,
                record.Title,
                record.Author,
                record.ThumbnailUrl,
                record.LocalFilePath,
                IsVideo = record.IsVideo ? 1 : 0,
                record.DurationSeconds,
                PlayedAtUtc = record.PlayedAtUtc.ToString("O")
            });

        await connection.ExecuteAsync(@"
            DELETE FROM PlaybackHistory
            WHERE Id NOT IN (
                SELECT Id FROM PlaybackHistory ORDER BY PlayedAtUtc DESC LIMIT @Limit
            );",
            new { Limit = AppGlobal.Transfers.MaxPlaybackHistoryItems });
    }

    public async Task ClearAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM PlaybackHistory;");
    }
}
