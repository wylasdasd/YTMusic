using System.Text.RegularExpressions;

namespace CommonHelp;

/// <summary>
/// 功能职责：多行文本统计（行数、非空、去重、单词、字符）。
/// </summary>
public sealed record TextLineStats(
    int LineCount,
    int NonEmptyLineCount,
    int UniqueLineCount,
    int WordCount,
    int CharCount,
    int CharNoWhitespaceCount);

/// <summary>
/// 功能职责：多行文本操作结果（处理后的文本与状态提示）。
/// </summary>
public sealed record TextLinesOperationResult(string Output, string StatusMessage);

/// <summary>
/// 功能职责：多行文本去重、排序、去空行、裁剪空白与行级统计。不负责文件读写。
/// 方法群组：统计、去重、排序、去空行、裁剪。
/// </summary>
public static partial class TextLinesHelp
{
    [GeneratedRegex(@"\p{L}[\p{L}\p{M}]*|\p{N}+")]
    private static partial Regex WordTokenRegex();
    /// <summary>
    /// 功能作用：统计行数、非空行数、去重行数、单词数与字符数。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <param name="applyTrim">统计去重前是否 trim 每行。</param>
    /// <param name="removeEmpty">去重统计是否忽略空行。</param>
    /// <param name="ignoreCase">去重是否忽略大小写。</param>
    /// <returns>统计快照。</returns>
    public static TextLineStats ComputeStats(string? text, bool applyTrim, bool removeEmpty, bool ignoreCase)
    {
        var rawLines = SplitLines(text ?? string.Empty);
        var lineCount = rawLines.Count;
        var nonEmptyLineCount = rawLines.Count(line => !string.IsNullOrWhiteSpace(line));
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        var normalizedLines = rawLines.Select(line => applyTrim ? line.Trim() : line);
        if (removeEmpty)
            normalizedLines = normalizedLines.Where(line => !string.IsNullOrWhiteSpace(line));

        var uniqueLineCount = new HashSet<string>(normalizedLines, comparer).Count;
        var charCount = text?.Length ?? 0;
        var charNoWhitespaceCount = string.IsNullOrEmpty(text) ? 0 : text.Count(c => !char.IsWhiteSpace(c));
        var wordCount = CountWords(text ?? string.Empty);
        return new TextLineStats(lineCount, nonEmptyLineCount, uniqueLineCount, wordCount, charCount, charNoWhitespaceCount);
    }

    /// <summary>
    /// 功能作用：多行文本去重。keepOrder 为 true 时保留首次出现顺序。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <param name="trimLines">是否先 trim 每行。</param>
    /// <param name="ignoreEmptyLines">是否去掉空行。</param>
    /// <param name="ignoreCase">是否忽略大小写。</param>
    /// <param name="keepOrder">是否保留首次出现顺序。</param>
    /// <returns>处理后的文本与状态提示。</returns>
    public static Result<TextLinesOperationResult> Deduplicate(
        string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase, bool keepOrder)
    {
        var lines = PrepareLines(text ?? string.Empty, trimLines, ignoreEmptyLines);
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        List<string> result;
        if (keepOrder)
        {
            result = new List<string>(lines.Count);
            var seen = new HashSet<string>(comparer);
            foreach (var line in lines)
            {
                if (seen.Add(line))
                    result.Add(line);
            }
        }
        else
        {
            result = lines.Distinct(comparer).ToList();
        }

        return Result<TextLinesOperationResult>.Ok(new TextLinesOperationResult(
            JoinLines(result),
            $"已去重 {lines.Count - result.Count} 行。"));
    }

    /// <summary>
    /// 功能作用：多行文本按字典序升序排序。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <param name="trimLines">是否先 trim 每行。</param>
    /// <param name="ignoreEmptyLines">是否去掉空行。</param>
    /// <param name="ignoreCase">是否忽略大小写。</param>
    /// <returns>处理后的文本与状态提示。</returns>
    public static Result<TextLinesOperationResult> SortAsc(string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase)
    {
        var lines = PrepareLines(text ?? string.Empty, trimLines, ignoreEmptyLines);
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        lines.Sort(comparer);
        return Result<TextLinesOperationResult>.Ok(new TextLinesOperationResult(JoinLines(lines), "已按升序排序。"));
    }

    /// <summary>
    /// 功能作用：多行文本按字典序降序排序。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <param name="trimLines">是否先 trim 每行。</param>
    /// <param name="ignoreEmptyLines">是否去掉空行。</param>
    /// <param name="ignoreCase">是否忽略大小写。</param>
    /// <returns>处理后的文本与状态提示。</returns>
    public static Result<TextLinesOperationResult> SortDesc(string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase)
    {
        var lines = PrepareLines(text ?? string.Empty, trimLines, ignoreEmptyLines);
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        lines.Sort(comparer);
        lines.Reverse();
        return Result<TextLinesOperationResult>.Ok(new TextLinesOperationResult(JoinLines(lines), "已按降序排序。"));
    }

    /// <summary>
    /// 功能作用：移除所有空白行。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <param name="trimLines">是否先 trim 每行。</param>
    /// <returns>处理后的文本与状态提示。</returns>
    public static Result<TextLinesOperationResult> RemoveEmptyLines(string? text, bool trimLines)
    {
        var lines = PrepareLines(text ?? string.Empty, trimLines, removeEmpty: true);
        return Result<TextLinesOperationResult>.Ok(new TextLinesOperationResult(JoinLines(lines), "已移除空行。"));
    }

    /// <summary>
    /// 功能作用：裁剪每行首尾空白（保留空行）。
    /// </summary>
    /// <param name="text">多行文本。</param>
    /// <returns>处理后的文本与状态提示。</returns>
    public static Result<TextLinesOperationResult> TrimOnly(string? text)
    {
        var lines = PrepareLines(text ?? string.Empty, applyTrim: true, removeEmpty: false);
        return Result<TextLinesOperationResult>.Ok(new TextLinesOperationResult(JoinLines(lines), "已裁剪行首尾空白。"));
    }

    /// <summary>
    /// 按选项预处理行列表（可选 trim 与去空行）。
    /// </summary>
    private static List<string> PrepareLines(string text, bool applyTrim, bool removeEmpty)
    {
        var lines = SplitLines(text);
        var list = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var value = applyTrim ? line.Trim() : line;
            if (removeEmpty && string.IsNullOrWhiteSpace(value))
                continue;

            list.Add(value);
        }

        return list;
    }

    /// <summary>
    /// 用系统换行符连接行列表；空列表返回空字符串。
    /// </summary>
    private static string JoinLines(IReadOnlyCollection<string> lines) =>
        lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);

    /// <summary>
    /// 单趟识别 \r\n、\r、\n 拆分为行列表（免去两次全文 Replace 中间分配）。
    /// </summary>
    private static List<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        var lines = new List<string>();
        var lineStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                lines.Add(text.Substring(lineStart, i - lineStart));
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                lineStart = i + 1;
            }
        }

        lines.Add(text.Substring(lineStart));
        return lines;
    }

    /// <summary>
    /// 按 Unicode 字母/数字类别统计单词数（支持中英文与数字混排，零分配计数）。
    /// </summary>
    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return WordTokenRegex().Count(text);
    }
}
