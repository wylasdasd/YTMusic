namespace YTMusic.BLL.Models;

public sealed class LocalFolderImportResult
{
    public FavoriteFolder Folder { get; init; } = new();
    public int TrackCount { get; init; }
}
