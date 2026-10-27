using YTMusic.BLL.Models;

namespace YTMusic.BLL.Ports;

public interface IFolderPicker
{
    Task<FolderPickResult> PickAsync(string title);
}

public sealed class FolderPickResult
{
    public bool IsSupported { get; init; }
    public string? Path { get; init; }
}
