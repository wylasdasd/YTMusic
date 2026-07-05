using System.Text.Json;

namespace YTMusic.BLL.Infrastructure.AList;

/// <summary>AList HTTP 客户端与 JSON 序列化配置（进程内共享）。</summary>
internal static class AListHttpClients
{
    public static HttpClient Default { get; } = new();

    public static HttpClient Upload { get; } = new()
    {
        // 默认 100s 超时在大文件/慢网上会中断 PUT，远端可能只留下半截文件。
        Timeout = TimeSpan.FromHours(6)
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
