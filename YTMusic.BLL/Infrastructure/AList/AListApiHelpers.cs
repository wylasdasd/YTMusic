using System.Net;
using System.Text.Json;

namespace YTMusic.BLL.Infrastructure.AList;

/// <summary>AList API 响应解析与路径编码工具。</summary>
internal static class AListApiHelpers
{
    public static string? GetDownloadUrl(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("message", out var messageElement))
        {
            return null;
        }

        var apiMessage = messageElement.GetString();
        if (!string.IsNullOrWhiteSpace(apiMessage) &&
            !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
            !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase) &&
            !apiMessage.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(apiMessage);
        }

        if (!document.RootElement.TryGetProperty("data", out var dataElement))
        {
            return null;
        }

        if (dataElement.TryGetProperty("raw_url", out var rawUrlElement))
        {
            var rawUrl = rawUrlElement.GetString();
            if (!string.IsNullOrWhiteSpace(rawUrl))
            {
                return rawUrl;
            }
        }

        if (dataElement.TryGetProperty("url", out var urlElement))
        {
            var url = urlElement.GetString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return null;
    }

    public static string EncodeRemotePath(string remotePath)
        => string.Join("/", remotePath
            .Split('/', StringSplitOptions.None)
            .Select(WebUtility.UrlEncode));

    public static string? GetApiMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public static string GetErrorMessage(string responseBody, string fallback)
        => GetApiMessage(responseBody) ?? fallback;

    public static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 502 or 503 or 504 or 429;
    }

    public static bool IsRetryableUploadException(Exception ex)
    {
        if (ex is InvalidOperationException invalidOperationException)
        {
            var message = invalidOperationException.Message;
            if (message.Contains("HTTP 502", StringComparison.Ordinal) ||
                message.Contains("HTTP 503", StringComparison.Ordinal) ||
                message.Contains("HTTP 504", StringComparison.Ordinal) ||
                message.Contains("HTTP 429", StringComparison.Ordinal) ||
                message.Contains("Upload size mismatch", StringComparison.Ordinal) ||
                message.Contains("Upload stream ended early", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return ex is IOException or HttpRequestException;
    }

    public static string GetRemoteParentPath(string remotePath)
    {
        var normalized = remotePath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }

    public static string GetRemoteFileName(string remotePath)
    {
        var normalized = remotePath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? normalized : normalized[(lastSlash + 1)..];
    }
}
