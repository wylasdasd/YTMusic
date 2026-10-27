namespace CommonHelp;

/// <summary>
/// 功能职责：剥 SSE 文本行的 <c>data:</c> 前缀。不解析 JSON、不识别 event/id 字段。
/// 方法群组：取 data 载荷。
/// </summary>
public static class SseLineHelp
{
    /// <summary>
    /// 功能作用：从一行取出 data 载荷。空行、非 data 行、空 data、以及 <c>[DONE]</c> 都视为没有。
    /// </summary>
    /// <param name="line">SSE 一行；可带首尾空白。</param>
    /// <param name="payload"><c>data:</c> 之后已 Trim 的文本。</param>
    /// <returns>取到非空且非 <c>[DONE]</c> 的载荷则为 true。</returns>
    public static bool TryGetDataPayload(string? line, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        var data = trimmed[5..].Trim();
        if (data.Length == 0 || data == "[DONE]")
            return false;

        payload = data;
        return true;
    }
}
