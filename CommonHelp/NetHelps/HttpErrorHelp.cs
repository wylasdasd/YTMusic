using System.Net;
using System.Text.Json;

namespace CommonHelp;

/// <summary>
/// 功能职责：从 JSON 错误体提取用户可读消息（error / error.message / message，兼容 OpenAI 风格）。不负责发请求。
/// 方法群组：格式化失败消息、提取错误体、聚合异常链。
/// </summary>
public static class HttpErrorHelp
{
    /// <summary>
    /// 功能作用：拼接用户可读失败消息。能提取到详情时为「prefix：HTTP 500，detail」，否则「prefix：HTTP 500。」。
    /// </summary>
    /// <param name="prefix">业务前缀。</param>
    /// <param name="code">HTTP 状态码。</param>
    /// <param name="responseText">响应正文。</param>
    /// <returns>失败说明文本。</returns>
    public static string FormatFailure(string prefix, HttpStatusCode code, string responseText)
    {
        var detail = TryExtractMessage(responseText);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{prefix}：HTTP {(int)code}。"
            : $"{prefix}：HTTP {(int)code}，{detail}";
    }

    /// <summary>
    /// 功能作用：按 InnerException 深度拼接异常链消息，跳过空消息与相邻重复；全部为空时返回异常类型名。
    /// </summary>
    /// <param name="ex">起始异常。</param>
    /// <param name="separator">消息分隔符，默认「 → 」。</param>
    /// <returns>聚合后的消息。</returns>
    public static string FormatExceptionChain(Exception ex, string separator = " → ")
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                (parts.Count == 0 || parts[^1] != current.Message))
            {
                parts.Add(current.Message);
            }
        }

        return parts.Count == 0 ? ex.GetType().Name : string.Join(separator, parts);
    }

    /// <summary>
    /// 功能作用：从 JSON 错误体提取消息。依次识别 error 字符串、error.message、message 字符串；解析失败或无匹配返回 null。
    /// </summary>
    /// <param name="responseText">响应正文。</param>
    /// <returns>提取到的消息；无匹配为 null。</returns>
    public static string? TryExtractMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var nestedMessage) &&
                    nestedMessage.ValueKind == JsonValueKind.String)
                {
                    return nestedMessage.GetString();
                }
            }

            if (root.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch
        {
        }

        return null;
    }
}
