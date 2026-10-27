using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：把 <see cref="HttpRequestMessage"/> 导出为可在终端执行的等价 curl 命令。不负责发送请求。
/// 方法群组：导出 curl。
/// </summary>
public static class HttpExtensions
{
    /// <summary>
    /// 功能作用：转换为 curl 命令字符串（含方法、URL、请求头；JSON/表单体用 -d，Multipart 仅占位）。
    /// </summary>
    /// <param name="request">HTTP 请求。</param>
    /// <returns>curl 命令文本。</returns>
    /// <remarks>异步：可等待。已发送过的请求体可能无法再读，失败时降级为占位。</remarks>
    public static async Task<string> ToCurlStringAsync(this HttpRequestMessage request)
    {
        var curl = new StringBuilder("curl");

        // 1. 设置请求方式
        curl.Append($" -X {request.Method.Method}");

        // 2. 设置 URL（单引号包裹，避免 $、反引号等被 shell 展开）
        curl.Append($" '{request.RequestUri}'");

        // 3. 处理请求头（值整体单引号包裹，内部单引号按 POSIX 规则转义）
        foreach (var header in request.Headers)
        {
            curl.Append($" -H '{EscapeSingleQuotes($"{header.Key}: {string.Join(", ", header.Value)}")}'");
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                curl.Append($" -H '{EscapeSingleQuotes($"{header.Key}: {string.Join(", ", header.Value)}")}'");
            }

            // 4. 处理 Body 内容
            var contentType = request.Content.Headers.ContentType?.MediaType;

            // 如果是 JSON 或表单，使用 -d 参数
            if (contentType != null && (contentType.Contains("json") || contentType.Contains("x-www-form-urlencoded")))
            {
                // 已发送过的请求内容可能无法再次读取，失败时降级为占位提示
                string body;
                try
                {
                    body = await request.Content.ReadAsStringAsync();
                }
                catch
                {
                    body = "[Content unreadable]";
                }

                curl.Append($" -d '{EscapeSingleQuotes(body)}'");
            }
            // 如果是 Multipart，建议仅打印关键字段提示（避免二进制乱码）
            else if (contentType != null && contentType.Contains("form-data"))
            {
                curl.Append(" -F '[Multipart Content - Check Logs for Details]'");
            }
        }

        return curl.ToString();
    }

    /// <summary>
    /// POSIX shell 单引号转义：' → '\''（闭合引号 + 转义引号 + 重开引号）。
    /// </summary>
    private static string EscapeSingleQuotes(string value) => value.Replace("'", "'\\''");
}
