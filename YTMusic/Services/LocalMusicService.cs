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
    public class LocalMusicService : ILocalMusicService
    {
        private readonly IFavoriteService _favoriteService;
        private readonly string _connectionString;

        public LocalMusicService(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
            
            SQLitePCL.Batteries_V2.Init();
            string dbDir = GetBaseDirectory();
            FileHelp.EnsureDirectoryExists(dbDir);
            string dbPath = Path.Combine(dbDir, "YTMusicDownloads.db3");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private string GetBaseDirectory()
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

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS DownloadedTracks (
                    VideoId TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    Author TEXT,
                    ThumbnailUrl TEXT,
                    LocalFilePath TEXT NOT NULL,
                    DownloadedDate DATETIME NOT NULL
                );";
            connection.Execute(createTableSql);
        }

        private string GetMusicDirectory()
        {
            return StoragePaths.GetDownloadedMusicDirectory();
        }

        public async Task<IReadOnlyList<DownloadedTrack>> GetDownloadedTracksAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            var tracks = await connection.QueryAsync<DownloadedTrack>("SELECT * FROM DownloadedTracks ORDER BY DownloadedDate DESC;");
            
            // Validate existence
            var validTracks = new List<DownloadedTrack>();
            foreach (var track in tracks)
            {
                if (File.Exists(track.LocalFilePath))
                {
                    validTracks.Add(track);
                }
                else
                {
                    // Optionally remove orphaned DB records here automatically
                    await RemoveDownloadedTrackAsync(track.VideoId, string.Empty);
                }
            }
            
            return validTracks;
        }

        public async Task AddDownloadedTrackAsync(DownloadedTrack track)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT OR REPLACE INTO DownloadedTracks (VideoId, Title, Author, ThumbnailUrl, LocalFilePath, DownloadedDate) 
                VALUES (@VideoId, @Title, @Author, @ThumbnailUrl, @LocalFilePath, @DownloadedDate);";
                
            await connection.ExecuteAsync(sql, track);
        }

        public async Task RemoveDownloadedTrackAsync(string videoId, string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync("DELETE FROM DownloadedTracks WHERE VideoId = @VideoId;", new { VideoId = videoId });
            
                FileHelp.DeleteIfExists(filePath);
        }

        public Task<IReadOnlyList<LocalAudioFile>> GetDownloadedAudioFilesAsync()
        {
            var directory = GetMusicDirectory();
            if (!Directory.Exists(directory))
            {
                return Task.FromResult<IReadOnlyList<LocalAudioFile>>(new List<LocalAudioFile>());
            }

            var files = Directory.GetFiles(directory);
            var result = files.Select(file => 
            {
                var fileName = Path.GetFileName(file);
                var title = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file);
                return new LocalAudioFile
                {
                    FileName = fileName,
                    FilePath = file,
                    Title = title,
                    Extension = extension
                };
            }).ToList();

            return Task.FromResult<IReadOnlyList<LocalAudioFile>>(result);
        }

        public async Task DeleteAudioFileAsync(string filePath)
        {
            FileHelp.DeleteIfExists(filePath);
            await _favoriteService.RemoveTrackByFilePathAsync(filePath);
        }
    }
}
