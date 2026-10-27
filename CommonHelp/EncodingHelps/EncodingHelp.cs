using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CommonHelp;

/// <summary>
/// 功能职责：常用文本编码表获取，以及 Hex 字符串与字节数组互转。不负责业务编码选择。
/// 方法群组：取 Encoding、解析 Hex、格式化 Hex。
/// </summary>
public static partial class EncodingHelp
{
    /// <summary>\xNN C 转义序列匹配（源生成正则，避免运行时解析模式）。</summary>
    [GeneratedRegex(@"\\x([0-9a-fA-F]{2})")]
    private static partial Regex HexEscapeRegex();
    /// <summary>
    /// 功能职责：编码选项（Id、展示名、传给 <see cref="Encoding.GetEncoding(string)"/> 的名称）。
    /// </summary>
    public sealed record EncodingOption(string Id, string Label, string CodePageName);

    /// <summary>文本 ↔ Hex 等工具常用编码。</summary>
    public static IReadOnlyList<EncodingOption> TextHexEncodings { get; } =
    [
        new("utf-8", "UTF-8", "utf-8"),
        new("utf-16le", "UTF-16 LE", "utf-16"),
        new("utf-16be", "UTF-16 BE", "utf-16BE"),
        new("ascii", "ASCII", "us-ascii"),
        new("latin1", "Latin-1 (ISO-8859-1)", "iso-8859-1"),
        new("gb2312", "GB2312", "gb2312"),
        new("gbk", "GBK", "gbk"),
        new("gb18030", "GB18030", "gb18030"),
        new("big5", "Big5（繁体）", "big5"),
    ];

    static EncodingHelp()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 功能作用：按编码 Id（如 "gbk"）取 Encoding；未知 Id 回退 UTF-8。静态构造会注册 CodePages 提供程序。
    /// </summary>
    /// <param name="id">编码 Id。</param>
    /// <returns>对应 Encoding；未知则 UTF-8。</returns>
    public static Encoding GetEncoding(string id)
    {
        var option = TextHexEncodings.FirstOrDefault(o =>
            string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

        return option is null
            ? Encoding.UTF8
            : Encoding.GetEncoding(option.CodePageName);
    }

    /// <summary>
    /// 功能作用：把十六进制字符串解析为字节；容忍 0x/\x 前缀、空格、-、, 与 \xNN C 转义。expectedSize 大于 0 时校验字节数。
    /// </summary>
    /// <param name="input">Hex 文本。</param>
    /// <param name="expectedSize">期望字节数；0 表示不校验。</param>
    /// <returns>解析得到的字节数组。</returns>
    public static byte[] ParseHexBytes(string input, int expectedSize = 0)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("请输入 Hex 内容。");

        byte[] bytes;
        if (input.Contains("\\x", StringComparison.OrdinalIgnoreCase) &&
            HexEscapeRegex().Matches(input) is { Count: > 0 } matches)
        {
            bytes = new byte[matches.Count];
            for (var i = 0; i < matches.Count; i++)
                bytes[i] = byte.Parse(matches[i].Groups[1].ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        else
        {
            // 单趟过滤：去空白与 -、, 分隔符，剔除 0x / \x 前缀标记（x 不是十六进制数字，游离的 x 保留并让 byte.Parse 报错）
            var sb = new StringBuilder(input.Length);
            var prevKept = '\0';
            foreach (var c in input)
            {
                if (char.IsWhiteSpace(c) || c is '-' or ',')
                    continue;

                if (c is 'x' or 'X' && prevKept is '0' or '\\')
                {
                    sb.Length--; // 移除前缀标记刚追加的 0 或 \ 字符
                    prevKept = '\0';
                    continue;
                }

                sb.Append(c);
                prevKept = c;
            }

            var cleaned = sb.ToString();
            if (cleaned.Length == 0)
                throw new FormatException("未找到有效 Hex 字节。");

            if (cleaned.Length % 2 != 0)
                cleaned = "0" + cleaned;

            bytes = new byte[cleaned.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(cleaned.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        if (expectedSize > 0 && bytes.Length != expectedSize)
            throw new FormatException($"期望 {expectedSize} 字节，实际 {bytes.Length} 字节。");

        return bytes;
    }

    /// <summary>
    /// 功能作用：把字节数组格式化为空格分隔的 Hex 字符串。
    /// </summary>
    /// <param name="bytes">字节。</param>
    /// <param name="uppercaseHex">是否大写。</param>
    /// <returns>空格分隔 Hex；空数组返回空串。</returns>
    public static string FormatHexBytes(byte[] bytes, bool uppercaseHex = true)
    {
        if (bytes.Length == 0)
            return string.Empty;

        return string.Create(bytes.Length * 3 - 1, (bytes, uppercaseHex), static (span, state) =>
        {
            var alphabet = state.uppercaseHex ? "0123456789ABCDEF" : "0123456789abcdef";
            var src = state.bytes;
            for (var i = 0; i < src.Length; i++)
            {
                if (i > 0)
                    span[i * 3 - 1] = ' ';

                var b = src[i];
                span[i * 3] = alphabet[b >> 4];
                span[i * 3 + 1] = alphabet[b & 0x0F];
            }
        });
    }
}
