using YTMusic.BLL.Ports;

namespace YTMusic.BLL.Infrastructure.FileSystem;

/// <summary>基于 <see cref="System.IO"/> 的 <see cref="IFileSystem"/> 实现。</summary>
public sealed class LocalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> GetFiles(string directory) => Directory.GetFiles(directory);
}
