using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommonHelp;
/// <summary>
/// 功能职责：文件与目录的创建、读写、复制、删除、遍历与大小统计。不负责业务路径约定、不负责跨进程锁（见 <see cref="LockFileHelper"/>）。
/// 方法群组：目录保障、读写文本/字节/流、复制删除、遍历与大小、文件名安全化、路径比较与根目录内解析。
/// </summary>
public static class FileHelp
{
    /// <summary>
    /// 人类可读的文件大小单位（B/KB/MB/GB/TB，按 1024 进位）。
    /// </summary>
    private static readonly string[] FileSizeUnits = ["B", "KB", "MB", "GB", "TB"];

    private static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    private static readonly EnumerationOptions RecursiveSafeEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    /// <summary>
    /// 功能作用：确保路径对应的目录存在。路径以分隔符结尾时创建该目录，否则视为文件路径并创建其父目录。
    /// </summary>
    /// <param name="path">文件路径，或以 <c>\</c> / <c>/</c> 结尾的目录路径。</param>
    /// <returns>无返回值；必要时创建目录。空白路径直接返回。</returns>
    public static void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var dir = path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
            return;

        Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// 功能作用：把任意流完整读入内存并返回字节数组（可寻址流先重置到起始位置）。
    /// </summary>
    /// <param name="stream">输入流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>流中的全部字节。</returns>
    /// <remarks>异步：可等待。</remarks>
    public static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        if (stream is MemoryStream memoryStream)
            return memoryStream.ToArray();

        var initialCapacity = stream.CanSeek && stream.Length is > 0 and <= int.MaxValue
            ? (int)stream.Length
            : 0;
        using var ms = new MemoryStream(initialCapacity);
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>
    /// 功能作用：把字节数格式化为人类可读大小（如 <c>1.5 MB</c>），按 1024 进位，最多两位小数。
    /// </summary>
    /// <param name="bytes">字节数。</param>
    /// <returns>带单位的文本。</returns>
    public static string FormatFileSize(long bytes)
    {
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < FileSizeUnits.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return $"{value:0.##} {FileSizeUnits[unitIndex]}";
    }

    /// <summary>
    /// 功能作用：文件不存在时创建空文件；已存在返回 false。必要时先建父目录。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <returns>实际创建了新文件则为 true。</returns>
    public static bool CreateFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            EnsureDirectoryExists(path);
            using var _ = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }

    /// <summary>
    /// 功能作用：同步写入全部文本（自动建目录，默认 UTF8，null 内容按空串）。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="content">要写入的文本；null 当空串。</param>
    /// <param name="encoding">可选编码，默认 UTF8。</param>
    /// <returns>无返回值；写入目标文件。</returns>
    public static void WriteAllText(string path, string content, Encoding? encoding = null)
    {
        EnsureDirectoryExists(path);
        File.WriteAllText(path, content ?? string.Empty, encoding ?? Encoding.UTF8);
    }

    /// <summary>
    /// 功能作用：异步写入全部文本（自动建目录，默认 UTF8）。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="content">要写入的文本；null 当空串。</param>
    /// <param name="encoding">可选编码，默认 UTF8。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>无返回值；写入目标文件。</returns>
    /// <remarks>异步：可等待。</remarks>
    public static async Task WriteAllTextAsync(string path, string content, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(path);
        encoding ??= Encoding.UTF8;
        await File.WriteAllTextAsync(path, content ?? string.Empty, encoding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 功能作用：同步读取全部文本；文件或目录不存在返回空串。
    /// </summary>
    /// <param name="path">源文件路径。</param>
    /// <param name="encoding">可选编码，默认 UTF8。</param>
    /// <returns>文件内容；缺失时返回空串。</returns>
    public static string ReadAllText(string path, Encoding? encoding = null)
    {
        try
        {
            return File.ReadAllText(path, encoding ?? Encoding.UTF8);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 功能作用：异步读取全部文本；文件或目录不存在返回空串。
    /// </summary>
    /// <param name="path">源文件路径。</param>
    /// <param name="encoding">可选编码，默认 UTF8。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>文件内容；缺失时返回空串。</returns>
    /// <remarks>异步：可等待。</remarks>
    public static async Task<string> ReadAllTextAsync(string path, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await File.ReadAllTextAsync(path, encoding ?? Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 功能作用：文件存在时删除；删除异常被吞掉并返回 false。
    /// </summary>
    /// <param name="path">要删除的文件路径。</param>
    /// <returns>实际删除则为 true。</returns>
    public static bool DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // swallow exceptions for safe delete
        }
        return false;
    }

    /// <summary>
    /// 功能作用：复制文件到目标位置，必要时创建目标目录。默认覆盖。
    /// </summary>
    /// <param name="sourcePath">源文件路径。</param>
    /// <param name="destPath">目标文件路径。</param>
    /// <param name="overwrite">目标已存在是否覆盖。</param>
    /// <returns>无返回值；写入目标文件。</returns>
    public static void CopyFile(string sourcePath, string destPath, bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath))
            throw new ArgumentException("Source or destination path is invalid.");

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file not found.", sourcePath);

        EnsureDirectoryExists(destPath);
        File.Copy(sourcePath, destPath, overwrite);
    }

    /// <summary>
    /// 功能作用：惰性递归枚举目录下所有文件；目录不存在返回空序列。
    /// </summary>
    /// <param name="directory">基目录。</param>
    /// <param name="searchPattern">搜索模式，默认 *.*。</param>
    /// <returns>匹配的文件路径；目录不存在为空序列。</returns>
    public static IEnumerable<string> GetFilesRecursively(string directory, string? searchPattern = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Enumerable.Empty<string>();

        searchPattern ??= "*.*";
        return Directory.EnumerateFiles(directory, searchPattern, RecursiveSafeEnumeration);
    }

    /// <summary>
    /// 功能作用：计算目录总大小（字节）；无法访问的文件忽略。
    /// </summary>
    /// <param name="directory">要计算的目录。</param>
    /// <param name="searchPattern">可选搜索模式。</param>
    /// <returns>总字节数；目录不存在为 0。</returns>
    public static long GetDirectorySize(string directory, string? searchPattern = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return 0;

        searchPattern ??= "*.*";
        long size = 0;
        try
        {
            foreach (var info in new DirectoryInfo(directory).EnumerateFiles(searchPattern, RecursiveSafeEnumeration))
            {
                try
                {
                    size += info.Length;
                }
                catch
                {
                    // ignore inaccessible files
                }
            }
        }
        catch
        {
            // ignore inaccessible directory root
        }
        return size;
    }

    /// <summary>
    /// 功能作用：把文件名中的非法字符替换为下划线。
    /// </summary>
    /// <param name="name">原始名称。</param>
    /// <returns>安全文件名；空输入返回空串。</returns>
    public static string SafeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (name.AsSpan().IndexOfAny(InvalidFileNameChars) < 0)
            return name;

        return string.Create(name.Length, name, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                span[i] = InvalidFileNameChars.Contains(c) ? '_' : c;
            }
        });
    }

    /// <summary>
    /// 功能作用：创建异步写入文件流（Asynchronous | SequentialScan）。调用方负责释放。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="bufferSize">缓冲区字节数，默认 81920。</param>
    /// <param name="overwrite">是否覆盖已有文件。</param>
    /// <returns>已打开的 <see cref="FileStream"/>。</returns>
    public static FileStream CreateFileStream(string path, int bufferSize = 81920, bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is null or empty", nameof(path));

        EnsureDirectoryExists(path);
        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        // Use Asynchronous and SequentialScan for best throughput on large writes
        return new FileStream(path, mode, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    /// <summary>
    /// 功能作用：异步把字节数组写入文件。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="data">要写入的字节。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>无返回值；写入目标文件。</returns>
    /// <remarks>异步：可等待。</remarks>
    public static async Task WriteBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        EnsureDirectoryExists(path);
        // bufferSize equals data length when small improves single-write performance
        var bufferSize = Math.Min(81920, Math.Max(4096, data.Length));
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await fs.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 功能作用：把源流从当前位置异步复制写入文件（不关闭源流）。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="source">源数据流，复制后不关闭。</param>
    /// <param name="overwrite">是否覆盖已有文件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>无返回值；写入目标文件。</returns>
    /// <remarks>异步：可等待。</remarks>
    public static async Task WriteStreamAsync(string path, Stream source, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        EnsureDirectoryExists(path);
        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var dest = new FileStream(path, mode, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        // CopyToAsync 从源流当前位置开始复制（不重置、不关闭源流）
        await source.CopyToAsync(dest, 81920, cancellationToken).ConfigureAwait(false);
        await dest.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 功能作用：用 WriteThrough 创建空文件并立即提交到磁盘（占位/标记用，较慢）。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <returns>无返回值；在磁盘上创建空文件。</returns>
    public static void CreateEmptyFileDurable(string path)
    {
        EnsureDirectoryExists(path);
        // Use small buffer and WriteThrough to force durability
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        fs.Flush(true);
    }

    /// <summary>
    /// 功能作用：判断两个文件系统路径是否指向同一位置（绝对化、统一分隔符、去掉末尾分隔符；Windows 忽略大小写）。
    /// </summary>
    /// <param name="pathA">路径甲；空白视为空路径。</param>
    /// <param name="pathB">路径乙；空白视为空路径。</param>
    /// <returns>规范化后相同则为 true；两个空白路径视为相同。非法路径不抛，退回斜杠统一后比较。</returns>
    public static bool AreSamePath(string? pathA, string? pathB)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(NormalizePathForCompare(pathA), NormalizePathForCompare(pathB), comparison);
    }

    /// <summary>
    /// 功能作用：把路径整理成可比较形式（Trim、尽量 <see cref="Path.GetFullPath"/>、分隔符统一、去掉末尾目录分隔符）。
    /// </summary>
    /// <param name="path">原始路径。</param>
    /// <returns>可比较字符串；空白输入返回空串。</returns>
    public static string NormalizePathForCompare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim();
        try
        {
            trimmed = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // 非法路径仍做分隔符统一，避免调用方比较时抛错。
        }

        return trimmed
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 功能作用：把相对路径接到根目录下得到绝对路径，拒绝跳出根目录（含 <c>..</c> 逃逸）。
    /// 输入：<paramref name="root"/> 允许的根目录；<paramref name="relativePath"/> 相对路径，若已是绝对路径则须仍落在根内。
    /// 输出：成功时 <paramref name="fullPath"/> 为规范化绝对路径；失败返回 false（空白、非法、越界都不抛）。
    /// </summary>
    /// <param name="root">根目录。</param>
    /// <param name="relativePath">相对或根内绝对路径。</param>
    /// <param name="fullPath">解析后的绝对路径；失败时为空串。</param>
    /// <returns>路径合法且位于根目录内（含根自身）时为 true。</returns>
    public static bool TryResolveUnderRoot(string? root, string? relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            var rootFull = Path.GetFullPath(root.Trim());
            var combined = Path.IsPathRooted(relativePath.Trim())
                ? Path.GetFullPath(relativePath.Trim())
                : Path.GetFullPath(Path.Combine(rootFull, relativePath.Trim()));
            var rootNorm = NormalizePathForCompare(rootFull);
            var combinedNorm = NormalizePathForCompare(combined);
            if (string.IsNullOrEmpty(rootNorm) || string.IsNullOrEmpty(combinedNorm))
                return false;

            if (AreSamePath(rootNorm, combinedNorm))
            {
                fullPath = combined;
                return true;
            }

            var prefix = rootNorm + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!combinedNorm.StartsWith(prefix, comparison))
                return false;

            fullPath = combined;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }
}


/// <summary>
/// 功能职责：文件独占锁。进程内按规范化绝对路径排队，跨进程 <see cref="FileShare.None"/>。不解析内容，由调用方通过 Action/Func 自行处理 FileStream。不要 Dispose 回调里的 stream。
/// 方法群组：锁内执行。
/// </summary>
public static class LockFileHelper
{
    private static readonly ConcurrentDictionary<string, object> FileLocks
        = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// 功能作用：独占打开文件（不存在则创建空文件），锁内执行 action，结束后关闭流并释放锁。打开后 Position == 0。
    /// </summary>
    /// <param name="filePath">目标文件路径。</param>
    /// <param name="action">自定义处理。请自行 Seek，不要假设读完后仍在文件头。</param>
    /// <returns>无返回值；副作用在 action 内发生。</returns>
    public static void WithExclusiveLock(string filePath, Action<FileStream> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        WithExclusiveLock(filePath, stream =>
        {
            action(stream);
            return true;
        });
    }

    /// <summary>
    /// 功能作用：独占打开文件（不存在则创建空文件），锁内执行 func 并返回其结果。
    /// </summary>
    /// <param name="filePath">目标文件路径。</param>
    /// <param name="func">自定义处理。进入时指针在 0；覆盖整文件须先 Seek 再写，写完用 SetLength 截尾。</param>
    /// <returns>func 的返回值。</returns>
    public static TResult WithExclusiveLock<TResult>(string filePath, Func<FileStream, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        filePath = NormalizeLockPath(filePath);
        FileHelp.EnsureDirectoryExists(filePath);
        var fileLock = FileLocks.GetOrAdd(filePath, _ => new object());
        lock (fileLock)
        {
            using var stream = new FileStream(
                filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // OpenOrCreate + ReadWrite：新建或已有文件，Position 均为 0（不是追加打开，不会停在 Length）。
            return func(stream);
        }
    }

    private static string NormalizeLockPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.GetFullPath(filePath);
    }
}
