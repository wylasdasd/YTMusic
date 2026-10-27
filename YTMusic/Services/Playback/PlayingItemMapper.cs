using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YTMusic.BLL.Models;

namespace YTMusic.Services.Playback;

/// <summary>
/// Single place that builds <see cref="PlayingItem"/> from domain / search models.
/// </summary>
public static class PlayingItemMapper
{
    public static PlayingItem From(FavoriteTrack track, bool includeLocalPath = true)
    {
        return new PlayingItem
        {
            VideoId = track.VideoId,
            Title = track.Title,
            Author = track.Author,
            ThumbnailUrl = track.ThumbnailUrl,
            LocalFilePath = includeLocalPath ? track.LocalFilePath : null,
            IsVideo = false
        };
    }

    public static PlayingItem From(DownloadedTrack track, bool? isVideo = false)
    {
        return new PlayingItem
        {
            VideoId = track.VideoId,
            Title = track.Title,
            Author = track.Author,
            ThumbnailUrl = track.ThumbnailUrl,
            LocalFilePath = track.LocalFilePath,
            IsVideo = isVideo
        };
    }

    public static PlayingItem From(PlaybackHistoryRecord record)
    {
        return new PlayingItem
        {
            VideoId = record.VideoId,
            Title = record.Title,
            Author = record.Author,
            ThumbnailUrl = record.ThumbnailUrl,
            LocalFilePath = record.LocalFilePath,
            IsVideo = record.IsVideo,
            DurationSeconds = record.DurationSeconds
        };
    }

    public static PlayingItem From(VideoSearchResult video)
    {
        return FromSearch(
            video.Id.Value,
            video.Title,
            video.Author.ChannelTitle,
            video.Thumbnails.FirstOrDefault()?.Url,
            video.Duration?.TotalSeconds);
    }

    public static PlayingItem From(IVideo video)
    {
        return FromSearch(
            video.Id.Value,
            video.Title,
            video.Author.ChannelTitle,
            video.Thumbnails.FirstOrDefault()?.Url,
            video.Duration?.TotalSeconds);
    }

    public static PlayingItem FromSearch(
        string videoId,
        string title,
        string author,
        string? thumbnailUrl = null,
        double? durationSeconds = null,
        bool? isVideo = null,
        string? localFilePath = null)
    {
        return new PlayingItem
        {
            VideoId = videoId,
            Title = title,
            Author = author,
            ThumbnailUrl = thumbnailUrl,
            DurationSeconds = durationSeconds,
            IsVideo = isVideo,
            LocalFilePath = localFilePath
        };
    }

    public static PlayingItem FromLocalFile(
        string filePath,
        string title,
        bool? isVideo = null,
        string? author = null,
        string? thumbnailUrl = null,
        string? videoId = null)
    {
        return new PlayingItem
        {
            VideoId = !string.IsNullOrWhiteSpace(videoId) ? videoId : "local",
            Title = title,
            Author = string.IsNullOrWhiteSpace(author) ? "Local File" : author,
            ThumbnailUrl = thumbnailUrl,
            LocalFilePath = filePath,
            IsVideo = isVideo
        };
    }

    public static PlayingItem Clone(PlayingItem source, bool? isVideo = null)
    {
        return new PlayingItem
        {
            VideoId = source.VideoId,
            Title = source.Title,
            Author = source.Author,
            ThumbnailUrl = source.ThumbnailUrl,
            LocalFilePath = source.LocalFilePath,
            DurationSeconds = source.DurationSeconds,
            IsVideo = isVideo ?? source.IsVideo
        };
    }

    public static List<PlayingItem> FromFavoriteTracks(
        IEnumerable<FavoriteTrack> tracks,
        bool forceIncludeLocalPath = false,
        bool requireExistingLocalFile = false)
    {
        var items = tracks.Select(track =>
        {
            var includePath = forceIncludeLocalPath || track.IsDownloaded;
            return From(track, includePath);
        });

        if (!requireExistingLocalFile)
        {
            return items.ToList();
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.LocalFilePath) && File.Exists(item.LocalFilePath))
            .ToList();
    }

    public static List<PlayingItem> FromDownloadedTracks(IEnumerable<DownloadedTrack> tracks, bool? isVideo = false)
    {
        return tracks.Select(track => From(track, isVideo)).ToList();
    }
}
