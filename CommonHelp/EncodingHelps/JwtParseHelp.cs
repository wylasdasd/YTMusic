using System.Text;
using System.Text.Json;

namespace CommonHelp;

/// <summary>
/// 功能职责：JWT 解析结果（缩进 Header/Payload JSON 与过期时间文本）。
/// </summary>
public sealed record JwtParseResult(
    string HeaderJson,
    string PayloadJson,
    string ExpiryUtc,
    string ExpiryLocal);

/// <summary>
/// 功能职责：解码 JWT Header/Payload 为缩进 JSON，并提取 exp。仅解码，不校验签名。
/// 方法群组：解析 token。
/// </summary>
public static class JwtParseHelp
{
    /// <summary>缩进输出选项；缓存静态实例，避免每段解码重建序列化元数据。</summary>
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// 功能作用：解析 JWT（Header.Payload[.Signature]），返回格式化 JSON 与过期时间；至少需要两段。
    /// </summary>
    /// <param name="token">JWT 字符串。</param>
    /// <returns>成功时携带 Header/Payload 与过期文本；格式错误则 Fail。</returns>
    public static Result<JwtParseResult> Parse(string? token)
    {
        var trimmed = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Result<JwtParseResult>.Fail("请输入 JWT。");

        var parts = trimmed.Split('.');
        if (parts.Length < 2)
            return Result<JwtParseResult>.Fail("JWT 格式不正确，需要至少包含 Header 和 Payload。");

        try
        {
            var headerJson = DecodePart(parts[0]);
            var payloadJson = DecodePart(parts[1]);

            var expiryUtc = string.Empty;
            var expiryLocal = string.Empty;
            if (TryReadExpiry(payloadJson, out var expiry))
            {
                expiryUtc = expiry.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                expiryLocal = expiry.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return Result<JwtParseResult>.Ok(new JwtParseResult(
                headerJson,
                payloadJson,
                expiryUtc,
                expiryLocal));
        }
        catch (Exception ex)
        {
            return Result<JwtParseResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 解码 JWT 的一段 Base64Url 内容为缩进 JSON 字符串。
    /// </summary>
    private static string DecodePart(string base64Url)
    {
        var jsonBytes = Convert.FromBase64String(Base64Help.FromUrlSafe(base64Url));
        var json = Encoding.UTF8.GetString(jsonBytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.JsonSe(IndentedOptions);
    }

    /// <summary>
    /// 从 Payload JSON 中读取 exp 声明（Unix 秒，接受数字或数字字符串）为 DateTimeOffset；缺失时返回 false。
    /// </summary>
    private static bool TryReadExpiry(string payloadJson, out DateTimeOffset expiry)
    {
        expiry = default;
        if (string.IsNullOrWhiteSpace(payloadJson))
            return false;

        using var doc = JsonDocument.Parse(payloadJson);
        if (!doc.RootElement.TryGetProperty("exp", out var expElement))
            return false;

        if (expElement.ValueKind == JsonValueKind.Number && expElement.TryGetInt64(out var seconds))
        {
            expiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }

        if (expElement.ValueKind == JsonValueKind.String && long.TryParse(expElement.GetString(), out seconds))
        {
            expiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }

        return false;
    }
}
