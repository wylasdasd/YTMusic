using YTMusic.BLL.Models;

namespace YTMusic.BLL.Abstractions;

public interface ILocalFolderImportService
{
    /// <summary>扫描目录并新建一个收藏夹。没有音频时返回 null，不创建空夹。</summary>
    Task<LocalFolderImportResult?> ImportAsync(string directoryPath);
}
