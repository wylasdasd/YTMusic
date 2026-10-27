using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CommonHelp;

/// <summary>
/// 功能职责：通过 stdio 启动并访问单个 MCP Server。不负责管理多个 Server 或 Agent 编排。
/// initialize 的 clientInfo 名称与版本由构造参数传入，不读本库程序集。
/// 方法群组：初始化、工具查询、资源释放。
/// </summary>
public sealed class StdioMcpClient : IAsyncDisposable
{
    private readonly string _serverName;
    private readonly McpServerOptions _options;
    private readonly string _clientName;
    private readonly string _clientVersion;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private StreamReader? _output;
    private int _requestId;

    /// <summary>
    /// 功能作用：记下要启动的 MCP Server，以及握手时上报的客户端身份。不启动进程。
    /// </summary>
    /// <param name="serverName">配置里的 server 名，用于错误信息。</param>
    /// <param name="options">启动命令、参数与工作目录。</param>
    /// <param name="clientName">initialize 的 clientInfo.name，由宿主产品传入。</param>
    /// <param name="clientVersion">initialize 的 clientInfo.version，由宿主产品传入。</param>
    public StdioMcpClient(string serverName, McpServerOptions options, string clientName, string clientVersion)
    {
        _serverName = serverName;
        _options = options;
        if (string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("MCP clientInfo 名称不能为空。", nameof(clientName));
        if (string.IsNullOrWhiteSpace(clientVersion))
            throw new ArgumentException("MCP clientInfo 版本不能为空。", nameof(clientVersion));
        _clientName = clientName.Trim();
        _clientVersion = clientVersion.Trim();
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            var result = await SendRequestAsync("tools/list", WriteEmptyObject, cancellationToken).ConfigureAwait(false);
            if (!result.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
                return [];

            return tools.EnumerateArray()
                .Select(tool => new McpToolInfo(
                    _serverName,
                    tool.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                    tool.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                    tool.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : "{}"))
                .Where(tool => !string.IsNullOrWhiteSpace(tool.Name))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JsonElement> CallToolAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            var result = await SendRequestAsync(
                "tools/call",
                writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", toolName);
                    writer.WritePropertyName("arguments");
                    arguments.WriteTo(writer);
                    writer.WriteEndObject();
                },
                cancellationToken).ConfigureAwait(false);
            return result.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false })
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.Command,
            WorkingDirectory = ResolveWorkingDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in _options.Args)
            startInfo.ArgumentList.Add(argument);

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"无法启动 MCP Server：{_serverName}");
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _ = DrainErrorsAsync(_process.StandardError);

        await SendRequestAsync("initialize", writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("protocolVersion", "2025-06-18");
            writer.WritePropertyName("capabilities");
            WriteEmptyObject(writer);
            writer.WritePropertyName("clientInfo");
            writer.WriteStartObject();
            writer.WriteString("name", _clientName);
            writer.WriteString("version", _clientVersion);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }, cancellationToken).ConfigureAwait(false);
        await SendNotificationAsync("notifications/initialized", WriteEmptyObject, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        Action<Utf8JsonWriter> writeParams,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        await WriteMessageAsync(id, method, writeParams, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var line = await _output!.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException($"MCP Server 已关闭输出：{_serverName}");
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
            {
                if (root.TryGetProperty("error", out var error))
                    throw new InvalidOperationException($"MCP 调用失败：{error.GetRawText()}");
                return root.GetProperty("result").Clone();
            }
        }
    }

    private async Task SendNotificationAsync(
        string method,
        Action<Utf8JsonWriter> writeParams,
        CancellationToken cancellationToken) =>
        await WriteMessageAsync(null, method, writeParams, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 功能作用：用手写 JSON 发出一条 JSON-RPC 消息。请求带 id，通知不带 id。
    /// </summary>
    /// <param name="id">请求序号；null 表示通知。</param>
    /// <param name="method">JSON-RPC 方法名。</param>
    /// <param name="writeParams">写入 params 对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>异步：可等待。写完并刷新 stdin 后返回。</remarks>
    private async Task WriteMessageAsync(
        int? id,
        string method,
        Action<Utf8JsonWriter> writeParams,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            if (id is int requestId)
                writer.WriteNumber("id", requestId);
            writer.WriteString("method", method);
            writer.WritePropertyName("params");
            writeParams(writer);
            writer.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        await _input!.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteEmptyObject(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private string ResolveWorkingDirectory() =>
        string.IsNullOrWhiteSpace(_options.WorkingDirectory)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(_options.WorkingDirectory);

    private static async Task DrainErrorsAsync(StreamReader errors)
    {
        while (await errors.ReadLineAsync().ConfigureAwait(false) is not null)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _input?.Close();
        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        _gate.Dispose();
        await Task.CompletedTask;
    }
}
