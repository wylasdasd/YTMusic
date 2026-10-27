using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonHelp;

/// <summary>
/// 功能职责：剥离 Markdown 代码围栏并规范化 JSON 文本（LLM 输出清洗常用）。不负责业务字段解析。
/// 方法群组：剥围栏、规范化 JSON。
/// </summary>
public static partial class MarkdownJsonHelp
{
    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownFenceRegex();

    /// <summary>
    /// 功能作用：剥离 ```/```json 围栏；命中时返回围栏内文本（已 Trim），否则返回原文本 Trim。
    /// </summary>
    /// <param name="text">可能带围栏的文本。</param>
    /// <returns>围栏内或原文本（已 Trim）。</returns>
    public static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        var match = MarkdownFenceRegex().Match(trimmed);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    /// <summary>
    /// 功能作用：剥围栏后解析并重新序列化为缩进、非 ASCII 不转义的展示格式；输入为空或解析失败时抛出。
    /// </summary>
    /// <param name="raw">可能带围栏的 JSON 文本。</param>
    /// <returns>规范化后的 JSON 文本。</returns>
    public static string NormalizeToJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("输入为空，无法解析为 JSON。");

        var text = StripCodeFence(raw);

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.JsonSe(JsonExtensions.IndentedRelaxedOptions);
    }
}
