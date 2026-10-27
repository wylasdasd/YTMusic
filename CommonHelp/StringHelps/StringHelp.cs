using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：文本行数、字符数/字节数统计（字符串与文件两种入口）以及截断加省略号。不负责改文件内容。
/// 方法群组：计行、计字符/字节、截断。
/// </summary>
public static class StringHelp
{
    /// <summary>
    /// 功能作用：计算文本行数。默认识别 \r\n、\r、\n 逻辑行，也可指定自定义分隔符。
    /// </summary>
    /// <param name="text">输入文本。</param>
    /// <param name="lineSeparator">自定义分隔符；null 按逻辑行计数。</param>
    /// <returns>行数；空文本为 0。</returns>
    public static int CountLines(string text, string? lineSeparator = null)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // 默认按逻辑行计数（\r\n、\r、\n 均识别，无分配），避免 Windows 下按 \r\n Split 数错纯 \n 文本
        if (lineSeparator is null)
            return (int)CountLogicalLines(text);

        // 自定义分隔符：无分配顺序计数
        var count = 1;
        var index = 0;
        while ((index = text.IndexOf(lineSeparator, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += lineSeparator.Length;
        }

        return count;
    }

    /// <summary>
    /// 功能作用：计算字符数或字节数。encoding 为 null 返回字符数，否则返回该编码下的字节数。
    /// </summary>
    /// <param name="text">输入文本。</param>
    /// <param name="encoding">用于计算字节数的编码；null 则返回字符数。</param>
    /// <returns>字符数或字节数；空文本为 0。</returns>
    public static long CountCharacters(string text, Encoding? encoding = null)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        if (encoding == null)
            return text.Length;
        return encoding.GetByteCount(text);
    }

    /// <summary>
    /// 功能作用：读取文件并计算行数与字符/字节数。encoding 为 null 按时字符数；文件不存在返回 (0, 0)。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <param name="encoding">字节统计用编码；null 按字符数。</param>
    /// <returns>行数与字符/字节数。</returns>
    public static (long Lines, long Count) CountFileLinesAndChars(string filePath, Encoding? encoding = null)
    {
        var text = FileHelp.ReadAllText(filePath, encoding ?? Encoding.UTF8);
        var count = encoding is null ? text.Length : encoding.GetByteCount(text);
        return (CountLogicalLines(text), count);
    }

    /// <summary>
    /// 功能作用：计算文件的字符数（encoding 为 null）或指定编码下的字节数。文件不存在返回 0。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <param name="encoding">字节统计用编码；null 按字符数。</param>
    /// <returns>字符数或字节数。</returns>
    public static long CountFileCharacters(string filePath, Encoding? encoding = null)
    {
        var text = FileHelp.ReadAllText(filePath, encoding ?? Encoding.UTF8);
        return encoding is null ? text.Length : encoding.GetByteCount(text);
    }

    /// <summary>
    /// 功能作用：超过 maxLength 时截到该长度并加省略号；否则原样返回。
    /// </summary>
    /// <param name="text">任意文本；null 当空串。</param>
    /// <param name="maxLength">保留的最大字符数（不含省略号）。</param>
    /// <param name="ellipsis">省略标记，默认「…」。</param>
    /// <returns>截断后的文本；空输入返回空串。</returns>
    public static string TruncateWithEllipsis(string? text, int maxLength, string ellipsis = "…")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (text.Length <= maxLength)
            return text;
        return string.Concat(text.AsSpan(0, maxLength), ellipsis);
    }

    private static long CountLogicalLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        long lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                lines++;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                continue;
            }

            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }
}
