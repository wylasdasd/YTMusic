using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

/// <summary>
/// Local library catalog (downloads). Not a favorite folder.
/// </summary>
public interface ILibraryCatalogService
{
    Task<IReadOnlyList<LibraryItem>> GetDownloadedItemsAsync();
}
