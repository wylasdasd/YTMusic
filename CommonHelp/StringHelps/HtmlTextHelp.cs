using System.Net;
using System.Text.RegularExpressions;

namespace CommonHelp;

/// <summary>
/// 功能职责：把 HTML 转成纯文本、抽取网页标题。按标签粗切，不做完整 DOM 解析，也不发 HTTP 请求。
/// 方法群组：正文转换、标题抽取。
/// </summary>
public static partial class HtmlTextHelp
{
    /// <summary>整块丢弃的元素（脚本 / 样式 / 注释等）；正则组 1 是标签名，供反向引用配对结束标签。</summary>
    [GeneratedRegex(@"<(script|style|noscript|svg|head|template)\b[^>]*>.*?</\1>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DroppedElementPattern();

    /// <summary>块级标签：替换成换行，避免整页粘成一行。</summary>
    [GeneratedRegex(@"</?(?:p|div|br|hr|li|ul|ol|tr|td|th|h[1-6]|section|article|aside|header|footer|nav|pre|blockquote|table|form|figure|figcaption)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakPattern();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitlePattern();

    /// <summary>行内空白（含解码出来的 &amp;nbsp;）压缩用。</summary>
    [GeneratedRegex(@"[ \t\r\f\v\u00A0]+")]
    private static partial Regex InlineSpacePattern();

    /// <summary>源文档空白：HTML 会把换行当普通空白渲染，先压成单空格再按块级标签切换行。</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunPattern();

    /// <summary>
    /// 功能作用：把 HTML 转成纯文本——丢脚本 / 样式 / 注释，源空白（含换行）压成单空格，块级标签转换行，解码实体，逐行去空白并丢弃空行。
    /// </summary>
    /// <param name="html">HTML 文本。</param>
    /// <returns>按 \n 分行的纯文本；null / 空白输入返回空串。</returns>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = DroppedElementPattern().Replace(html, " ");
        text = WhitespaceRunPattern().Replace(text, " ");
        text = BreakPattern().Replace(text, "\n");
        text = TagPattern().Replace(text, string.Empty);
        return CollapseLines(WebUtility.HtmlDecode(text));
    }

    /// <summary>
    /// 功能作用：抽取 &lt;title&gt; 的内容。
    /// </summary>
    /// <param name="html">HTML 文本。</param>
    /// <returns>解码并压缩空白后的标题；没有 title 标签时为空串。</returns>
    public static string ExtractTitle(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var match = TitlePattern().Match(html);
        return match.Success
            ? InlineSpacePattern().Replace(WebUtility.HtmlDecode(match.Groups["title"].Value), " ").Trim()
            : string.Empty;
    }

    private static string CollapseLines(string text)
    {
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var collapsed = InlineSpacePattern().Replace(line, " ").Trim();
            if (collapsed.Length > 0)
                kept.Add(collapsed);
        }

        return string.Join('\n', kept);
    }
}
