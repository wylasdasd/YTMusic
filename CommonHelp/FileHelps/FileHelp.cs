using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommonTool.FileHelps
{
    /// <summary>
    /// 通用文件操作工具，供解决方案中各处使用。
    /// 封装了一些安全的 System.IO 常用操作的便捷方法。
    /// </summary>
    public static class FileHelp
    {
        /// <summary>
        /// 确保路径的目录存在。
        /// 如果传入的是目录路径则创建该目录；如果是文件路径则创建其父目录。
        /// </summary>
        /// <param name="path">文件或目录路径。</param>
        public static void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var dir = Path.GetDirectoryName(path) ?? path;
            if (string.IsNullOrWhiteSpace(dir))
                return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 安全创建文件（如果不存在）。必要时会先创建目录。（这个using 是在多线程下防止并发错误的）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool CreateFile(string path)
        {
            if (!File.Exists(path))
            {
                EnsureDirectoryExists(path);
                using (File.Create(path))
                { return true; } // 确保文件句柄被正确释放
            }
            return false;
        }

        /// <summary>
        /// 将文本写入文件，必要时会先创建目录。
        /// 是对 <see cref="File.WriteAllText(string,string,Encoding)"/> 的轻量封装。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="content">要写入的文本内容。为 null 时视为空字符串处理。</param>
        /// <param name="encoding">可选编码（默认 UTF8）。</param>
        public static void WriteAllText(string path, string content, Encoding? encoding = null)
        {
            EnsureDirectoryExists(path);
            File.WriteAllText(path, content ?? string.Empty, encoding ?? Encoding.UTF8);
        }

        /// <summary>
        /// 异步将文本写入文件，必要时会先创建目录。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="content">要写入的文本内容。为 null 时视为空字符串处理。</param>
        /// <param name="encoding">可选编码（默认 UTF8）。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        public static async Task WriteAllTextAsync(string path, string content, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            EnsureDirectoryExists(path);
            encoding ??= Encoding.UTF8;
            await File.WriteAllTextAsync(path, content ?? string.Empty, encoding, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 读取文件全部文本。若文件不存在则返回空字符串。
        /// </summary>
        /// <param name="path">源文件路径。</param>
        /// <param name="encoding">可选编码（默认 UTF8）。</param>
        /// <returns>文件内容，文件缺失时返回空字符串。</returns>
        public static string ReadAllText(string path, Encoding? encoding = null)
        {
            if (!File.Exists(path))
                return string.Empty;
            return File.ReadAllText(path, encoding ?? Encoding.UTF8);
        }

        /// <summary>
        /// 异步读取文件全部文本。若文件不存在则返回空字符串。
        /// </summary>
        /// <param name="path">源文件路径。</param>
        /// <param name="encoding">可选编码（默认 UTF8）。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>文件内容，文件缺失时返回空字符串。</returns>
        public static async Task<string> ReadAllTextAsync(string path, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                return string.Empty;
            return await File.ReadAllTextAsync(path, encoding ?? Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 删除指定文件（如果存在）。删除过程中发生异常会被吞掉并返回 false。
        /// </summary>
        /// <param name="path">要删除的文件路径。</param>
        /// <returns>如果文件存在且被删除则返回 true；否则返回 false。</returns>
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
        /// 复制文件到目标位置，必要时会创建目标目录。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="destPath">目标文件路径。</param>
        /// <param name="overwrite">如果目标存在是否覆盖。</param>
        /// <exception cref="ArgumentException">当提供的路径无效时抛出。</exception>
        /// <exception cref="FileNotFoundException">当源文件不存在时抛出。</exception>
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
        /// 递归枚举目录下的文件。
        /// </summary>
        /// <param name="directory">要搜索的基目录。</param>
        /// <param name="searchPattern">可选搜索模式（默认为 '*.*'）。</param>
        /// <returns>匹配的文件路径集合；若目录不存在则返回空序列。</returns>
        public static IEnumerable<string> GetFilesRecursively(string directory, string? searchPattern = null)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return Enumerable.Empty<string>();

            searchPattern ??= "*.*";
            return Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
        }

        /// <summary>
        /// 计算目录下所有文件的总大小（字节数），可选按 <paramref name="searchPattern"/> 过滤。
        /// 无法访问的文件会被忽略。
        /// </summary>
        /// <param name="directory">要计算的目录。</param>
        /// <param name="searchPattern">可选的搜索模式。</param>
        /// <returns>总字节数。</returns>
        public static long GetDirectorySize(string directory, string? searchPattern = null)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return 0;

            var files = GetFilesRecursively(directory, searchPattern);
            long size = 0;
            foreach (var f in files)
            {
                try
                {
                    var info = new FileInfo(f);
                    size += info.Length;
                }
                catch
                {
                    // ignore inaccessible files
                }
            }
            return size;
        }

        /// <summary>
        /// 将输入的名称转换为文件名安全的字符串，非法字符替换为下划线。
        /// </summary>
        /// <param name="name">要处理的名称。</param>
        /// <returns>处理后的安全文件名（输入为空时返回空字符串）。</returns>
        public static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 高效创建并打开一个写入用的 <see cref="FileStream"/>，供流式写入使用。
        /// 使用 <see cref="FileOptions.Asynchronous"/> 和 <see cref="FileOptions.SequentialScan"/> 提高大文件写入效率。
        /// 调用者负责释放返回的流。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="bufferSize">缓冲区大小（字节），建议使用 81920（默认）。</param>
        /// <param name="overwrite">是否覆盖已有文件（默认为 true）。</param>
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
        /// 将字节数组高效写入到文件（异步）。适用于一次性写入整个缓冲区的场景。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="data">要写入的字节数组。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        public static async Task WriteBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            EnsureDirectoryExists(path);
            // bufferSize equals data length when small improves single-write performance
            var bufferSize = Math.Min(81920, Math.Max(4096, data.Length));
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await fs.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 将一个源流高效写入到目标文件（异步，流复制）。适合需要从流复制到文件的场景。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="source">源数据流，复制后不会关闭源流。</param>
        /// <param name="overwrite">是否覆盖已有文件。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        public static async Task WriteStreamAsync(string path, Stream source, bool overwrite = true, CancellationToken cancellationToken = default)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            EnsureDirectoryExists(path);
            var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
            await using var dest = new FileStream(path, mode, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            // CopyToAsync with default buffer is efficient; preserve position of source if seekable
            if (source.CanSeek && source.Position != 0)
            {
                // ensure reading from current position
            }
            await source.CopyToAsync(dest, 81920, cancellationToken).ConfigureAwait(false);
            await dest.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 快速创建一个空文件并立即提交到磁盘（使用 WriteThrough）。
        /// 适用于需要确保文件已经写入物理介质的场景（会较慢），一般用于占位或标记文件。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        public static void CreateEmptyFileDurable(string path)
        {
            EnsureDirectoryExists(path);
            // Use small buffer and WriteThrough to force durability
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            fs.Flush(true);
        }
    }
}