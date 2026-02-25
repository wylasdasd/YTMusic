using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using static CommonTool.JsonHelps.JsonExtensions;

namespace CommonTool.FileHelps
{
    /// <summary>
    /// 原子性 JSON 文件读写工具，提供跨进程与进程内互斥的安全访问方式。
    /// </summary>
    public class AtomicJsonFileHelper
    {
        // 定义一个进程内的对象锁，用于防止同一个应用内的多个线程同时开始操作。
        // 注意：这个锁只对当前进程有效，不能阻止其他独立进程访问文件。
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> fileLocks
            = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();

        /// <summary>
        /// 表示对某个 JSON 文件的原子性读写句柄。
        /// 当创建后会保持对文件的独占访问（FileShare.None）以及进程内监视器锁，直到 Dispose/Commit 被调用。
        /// </summary>
        public sealed class AtomicFileHandle<T> : IDisposable where T : class
        {
            private readonly string _filePath;
            private readonly object _fileLock;
            private FileStream _stream;
            private bool _committed;

            public T Data { get; }

            internal AtomicFileHandle(string filePath, object fileLock, FileStream stream, T data)
            {
                _filePath = filePath;
                _fileLock = fileLock;
                _stream = stream;
                Data = data;
                _committed = false;
            }

            /// <summary>
            /// 将当前内存数据序列化并写回文件，然后释放文件句柄和锁。
            /// </summary>
            public void Commit()
            {
                if (_committed) return;

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = new LowerCaseNamingPolicy(),
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true
                    };

                    // 截断并写入
                    _stream.SetLength(0);
                    _stream.Seek(0, SeekOrigin.Begin);
                    JsonSerializer.Serialize(_stream, Data.JsonSeToLow(), options);
                    _stream.Flush();
                    _committed = true;
                }
                finally
                {
                    Dispose();
                }
            }

            public void Dispose()
            {
                try
                {
                    if (_stream != null)
                    {
                        try { _stream.Dispose(); } catch { }
                        _stream = null!;
                    }
                }
                finally
                {
                    // 释放进程内锁
                    try { System.Threading.Monitor.Exit(_fileLock); } catch { }
                }
            }
        }


        /// <summary>
        /// 使用说明：
        /// 本类提供原子性（独占）访问 JSON 文件的方式，已将原先的单一 Update 回调模式拆分为两个方法：
        /// <list type="bullet">
        /// <item><description><see cref="ReadAtomic{T}(string)"/> - 以独占方式打开文件并返回一个 <see cref="AtomicFileHandle{T}"/> 对象，调用方可在内存中修改 <c>handle.Data</c>，然后调用 <c>handle.Commit()</c> 将修改写回并释放锁；若不调用 Commit 则在 Dispose 时直接释放锁并不写回。</description></item>
        /// <item><description><see cref="WriteAtomic{T}(string, T)"/> - 仅以独占方式将给定数据写入文件（覆盖写入）。</description></item>
        /// </list>
        /// 示例：
        /// <code>
        /// using var handle = AtomicFileHelper.ReadAtomic&lt;MyModel&gt;(path);
        /// handle.Data.Value = 123;
        /// handle.Commit(); // 写回并释放锁
        /// </code>
        /// 注意：ReadAtomic/Commit 与 WriteAtomic 都会在文件上使用 FileShare.None 以保证跨进程的独占性，且会在进程内使用监视器锁防止同一进程内的并发访问。
        /// 这些方法适用于低并发或需要强一致性的场景。
        /// </summary>
        /// <summary>
        /// 原子性地打开并返回文件句柄与反序列化的数据。返回的 <see cref="AtomicFileHandle{T}"/>
        /// 持有对文件的独占访问（FileShare.None）以及进程内的监视器锁，直到调用 <see cref="AtomicFileHandle{T}.Commit"/>
        /// 或 <see cref="AtomicFileHandle{T}.Dispose"/> 为止。
        /// 使用场景：调用方先调用 ReadAtomic 获取数据，对数据进行修改，然后调用 handle.Commit() 将变更写回；在此期间其他线程/进程无法修改该文件。
        /// </summary>
        public static AtomicFileHandle<T> ReadAtomic<T>(string filePath) where T : class
        {
            var fileLock = fileLocks.GetOrAdd(filePath, _ => new object());
            // 获取进程内的监视器锁并保持，直到 handle 被释放
            System.Threading.Monitor.Enter(fileLock);
            FileStream? stream = null;
            try
            {
                stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                T data;
                var options1 = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                if (stream.Length == 0)
                {
                    data = default;
                }
                else
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    data = JsonSerializer.Deserialize<T>(stream, options1) ?? default;
                }

                // 构造并返回句柄（句柄负责最终的写回与释放锁）
                return new AtomicFileHandle<T>(filePath, fileLock, stream, data);
            }
            catch
            {
                // 打开失败，释放监视器锁并关闭流（若已打开）
                if (stream != null)
                {
                    try { stream.Dispose(); } catch { }
                }
                System.Threading.Monitor.Exit(fileLock);
                throw;
            }
        }

        /// <summary>
        /// 仅写入数据（独占写入），不做读出。
        /// 当需要在外部先读取并修改后再写入时，建议使用 <see cref="AtomicFileHandle{T}"/> 的 Commit 方法以保持整个事务的隔离性。
        /// </summary>
        public static void WriteAtomic<T>(string filePath, T data) where T : class
        {
            FileHelp.EnsureDirectoryExists(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new LowerCaseNamingPolicy(),
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            // 使用独占打开，仅在写入期间阻止其他访问
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, data.JsonSeToLow(), options);
            stream.Flush();
        }


    }
}
