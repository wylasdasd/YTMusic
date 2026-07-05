namespace YTMusic.BLL.Ports;

/// <summary>本地文件系统访问抽象，便于 BLL 单测与替换存储后端。</summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    IReadOnlyList<string> GetFiles(string directory);
}
