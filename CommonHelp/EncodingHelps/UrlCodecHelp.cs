using System.Net;

namespace CommonHelp;

/// <summary>
/// 功能职责：URL 百分号编解码。提供 RFC 3986（Uri.EscapeDataString）与 WebUtility 两种模式。不负责拼完整 URL。
/// 方法群组：编码、解码。
/// </summary>
public static class UrlCodecHelp
{
    /// <summary>
    /// 功能作用：URL 编码。useEscapeDataString 为 true 时用 RFC 3986 严格编码，否则用 WebUtility 宽松编码。
    /// </summary>
    /// <param name="plainText">原文；null 当空串。</param>
    /// <param name="useEscapeDataString">是否用 Uri.EscapeDataString。</param>
    /// <returns>成功时为编码文本；异常则 Fail。</returns>
    public static Result<string> Encode(string? plainText, bool useEscapeDataString)
    {
        try
        {
            var input = plainText ?? string.Empty;
            var encoded = useEscapeDataString
                ? Uri.EscapeDataString(input)
                : WebUtility.UrlEncode(input);
            return Result<string>.Ok(encoded ?? string.Empty);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 功能作用：URL 解码；模式须与编码时一致。
    /// </summary>
    /// <param name="encodedText">已编码文本；null 当空串。</param>
    /// <param name="useEscapeDataString">是否用 Uri.UnescapeDataString。</param>
    /// <returns>成功时为原文；异常则 Fail。</returns>
    public static Result<string> Decode(string? encodedText, bool useEscapeDataString)
    {
        try
        {
            var input = encodedText ?? string.Empty;
            var decoded = useEscapeDataString
                ? Uri.UnescapeDataString(input)
                : WebUtility.UrlDecode(input);
            return Result<string>.Ok(decoded ?? string.Empty);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }
}
