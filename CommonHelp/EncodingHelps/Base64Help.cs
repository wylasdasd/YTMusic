using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：Base64 编解码，支持标准与 URL-safe 字母表。不负责文件/流。
/// 方法群组：编码、解码。
/// </summary>
public static class Base64Help
{
    /// <summary>
    /// 功能作用：把 UTF-8 文本编码为 Base64；可选 URL-safe 字母表。
    /// </summary>
    /// <param name="plainText">明文；null 当空串。</param>
    /// <param name="useUrlSafe">true 时用 -/_ 并去掉填充。</param>
    /// <returns>成功时为 Base64 文本；异常则 Fail。</returns>
    public static Result<string> Encode(string? plainText, bool useUrlSafe)
    {
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            var base64 = Convert.ToBase64String(bytes);
            var result = useUrlSafe ? ToUrlSafe(base64) : base64;
            return Result<string>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 功能作用：把 Base64 解码为 UTF-8 文本；URL-safe 时先还原字母表与填充。
    /// </summary>
    /// <param name="base64Text">Base64 文本；null 当空串。</param>
    /// <param name="useUrlSafe">须与编码时一致。</param>
    /// <returns>成功时为明文；格式错误则 Fail。</returns>
    public static Result<string> Decode(string? base64Text, bool useUrlSafe)
    {
        try
        {
            var input = base64Text ?? string.Empty;
            if (useUrlSafe)
                input = FromUrlSafe(input);

            var bytes = Convert.FromBase64String(input);
            return Result<string>.Ok(System.Text.Encoding.UTF8.GetString(bytes));
        }
        catch (FormatException ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 标准 Base64 转 URL-safe：+ → -、/ → _，并去除尾部 = 填充。
    /// </summary>
    private static string ToUrlSafe(string base64) =>
        base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>
    /// URL-safe 还原为标准 Base64：字符替换回退并按需补齐 = 填充。
    /// </summary>
    internal static string FromUrlSafe(string base64)
    {
        var restored = base64.Replace('-', '+').Replace('_', '/');
        var padding = 4 - (restored.Length % 4);
        if (padding is > 0 and < 4)
            restored = restored.PadRight(restored.Length + padding, '=');

        return restored;
    }
}
