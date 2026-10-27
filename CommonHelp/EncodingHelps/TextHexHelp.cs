using System.Globalization;
using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：文本转 Hex 结果（空格分隔 Hex 与按展示模式组装的完整输出）。
/// </summary>
public sealed record TextHexEncodeResult(string HexInput, string Output);

/// <summary>
/// 功能职责：Hex 转文本结果（两字段内容一致，便于双向绑定）。
/// </summary>
public sealed record TextHexDecodeResult(string TextInput, string Output);

/// <summary>
/// 功能职责：文本与十六进制互转。编码见 <see cref="EncodingHelp.TextHexEncodings"/>。不负责文件读写。
/// 方法群组：文本转 Hex、Hex 转文本。
/// </summary>
public static class TextHexHelp
{
    /// <summary>
    /// 功能作用：按指定编码把文本转为字节，输出空格分隔 Hex 及附加展示块（连续 Hex、\xNN、\uXXXX 按需叠加）。
    /// </summary>
    /// <param name="textInput">原文；null 当空串。</param>
    /// <param name="encodingId">编码 Id。</param>
    /// <param name="displayModeId">主展示模式（空格/continuous/c-escape/unicode）。</param>
    /// <param name="uppercaseHex">Hex 是否大写。</param>
    /// <param name="showUnicodeEscape">是否附加 Unicode 转义块。</param>
    /// <param name="showCEscape">是否附加 C 转义块。</param>
    /// <returns>成功时携带 Hex 与组装输出；异常则 Fail。</returns>
    public static Result<TextHexEncodeResult> EncodeTextToHex(
        string? textInput,
        string encodingId,
        string displayModeId,
        bool uppercaseHex,
        bool showUnicodeEscape,
        bool showCEscape)
    {
        try
        {
            var encoding = EncodingHelp.GetEncoding(encodingId);
            var bytes = encoding.GetBytes(textInput ?? string.Empty);
            var hexInput = EncodingHelp.FormatHexBytes(bytes, uppercaseHex);
            var output = BuildDisplay(bytes, encoding, displayModeId, uppercaseHex, showUnicodeEscape, showCEscape);
            return Result<TextHexEncodeResult>.Ok(new TextHexEncodeResult(hexInput, output));
        }
        catch (Exception ex)
        {
            return Result<TextHexEncodeResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 功能作用：把十六进制字符串（容忍 0x/\x 前缀与分隔符）按指定编码解码为文本。
    /// </summary>
    /// <param name="hexInput">Hex 文本。</param>
    /// <param name="encodingId">编码 Id。</param>
    /// <returns>成功时 TextInput 与 Output 均为解码文本；异常则 Fail。</returns>
    public static Result<TextHexDecodeResult> DecodeHexToText(string? hexInput, string encodingId)
    {
        try
        {
            var bytes = EncodingHelp.ParseHexBytes(hexInput ?? string.Empty);
            var encoding = EncodingHelp.GetEncoding(encodingId);
            var textInput = encoding.GetString(bytes);
            return Result<TextHexDecodeResult>.Ok(new TextHexDecodeResult(textInput, textInput));
        }
        catch (Exception ex)
        {
            return Result<TextHexDecodeResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 按展示模式组装主输出与附加输出块，块之间以空行分隔。
    /// </summary>
    private static string BuildDisplay(
        byte[] bytes,
        System.Text.Encoding encoding,
        string displayModeId,
        bool uppercaseHex,
        bool showUnicodeEscape,
        bool showCEscape)
    {
        var parts = new List<string>(3);
        var primary = displayModeId switch
        {
            "continuous" => uppercaseHex ? Convert.ToHexString(bytes) : Convert.ToHexStringLower(bytes),
            "c-escape" => FormatCEscape(bytes, uppercaseHex),
            "unicode" => BuildUnicodeEscape(bytes, encoding),
            _ => EncodingHelp.FormatHexBytes(bytes, uppercaseHex),
        };
        parts.Add(primary);

        if (showCEscape && displayModeId != "c-escape")
            parts.Add(FormatCEscape(bytes, uppercaseHex));

        if (showUnicodeEscape && displayModeId != "unicode")
            parts.Add(BuildUnicodeEscape(bytes, encoding));

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string FormatCEscape(byte[] bytes, bool uppercaseHex)
    {
        if (bytes.Length == 0)
            return string.Empty;

        return string.Create(bytes.Length * 4, (bytes, uppercaseHex), static (span, state) =>
        {
            var alphabet = state.uppercaseHex ? "0123456789ABCDEF" : "0123456789abcdef";
            var src = state.bytes;
            for (var i = 0; i < src.Length; i++)
            {
                var offset = i * 4;
                var b = src[i];
                span[offset] = '\\';
                span[offset + 1] = 'x';
                span[offset + 2] = alphabet[b >> 4];
                span[offset + 3] = alphabet[b & 0x0F];
            }
        });
    }

    /// <summary>
    /// 把解码文本中的非 ASCII 与控制字符转为 \uXXXX 转义表示。
    /// </summary>
    private static string BuildUnicodeEscape(byte[] bytes, System.Text.Encoding encoding)
    {
        var text = encoding.GetString(bytes);
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch <= 0x7F && !char.IsControl(ch))
                sb.Append(ch);
            else
                sb.Append(CultureInfo.InvariantCulture, $"\\u{((int)ch):X4}");
        }

        return sb.ToString();
    }
}
