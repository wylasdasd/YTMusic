namespace CommonHelp;

/// <summary>Cursor 兼容的外部 MCP Server 启动配置。</summary>
public sealed record McpServerOptions(
    string Command,
    IReadOnlyList<string> Args,
    string? WorkingDirectory = null,
    bool Enabled = true);

/// <summary>Cursor 兼容的 MCP 配置文件。</summary>
public sealed record McpConfiguration(
    IReadOnlyDictionary<string, McpServerOptions> McpServers);

/// <summary>从 MCP Server 返回的工具描述。</summary>
public sealed record McpToolInfo(
    string ServerName,
    string Name,
    string Description,
    string InputSchemaJson);
