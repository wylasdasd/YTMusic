namespace YTMusic.BLL.Ports;

public sealed class FavoritePickRequest
{
    public string VideoId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? LocalFilePath { get; init; }
}

public sealed class PickedFile
{
    public string FileName { get; init; } = string.Empty;
    public string? FullPath { get; init; }
}
