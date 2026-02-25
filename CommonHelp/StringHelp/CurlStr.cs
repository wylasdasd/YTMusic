using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTool.StringHelp
{
    public static class HttpExtensions
    {
        public static async Task<string> ToCurlStringAsync(this HttpRequestMessage request)
        {
            var curl = new StringBuilder("curl");

            // 1. 设置请求方式
            curl.Append($" -X {request.Method.Method}");

            // 2. 设置 URL
            curl.Append($" \"{request.RequestUri}\"");

            // 3. 处理请求头
            foreach (var header in request.Headers)
            {
                curl.Append($" -H \"{header.Key}: {string.Join(", ", header.Value)}\"");
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    curl.Append($" -H \"{header.Key}: {string.Join(", ", header.Value)}\"");
                }

                // 4. 处理 Body 内容
                var contentType = request.Content.Headers.ContentType?.MediaType;

                // 如果是 JSON 或表单，使用 -d 参数
                if (contentType != null && (contentType.Contains("json") || contentType.Contains("x-www-form-urlencoded")))
                {
                    var body = await request.Content.ReadAsStringAsync();
                    // 转义双引号以防在 shell 中报错
                    curl.Append($" -d \"{body.Replace("\"", "\\\"")}\"");
                }
                // 如果是 Multipart，建议仅打印关键字段提示（避免二进制乱码）
                else if (contentType != null && contentType.Contains("form-data"))
                {
                    curl.Append(" -F \"[Multipart Content - Check Logs for Details]\"");
                }
            }

            return curl.ToString();
        }
    }
}