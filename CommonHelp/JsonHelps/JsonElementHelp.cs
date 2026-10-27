using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace CommonHelp;

/// <summary>
/// 功能职责：从 <see cref="JsonElement"/> 取常用标量与容器。不解析文档、不识别业务字段名。
/// 方法群组：按属性名取值、按节点取值。
/// </summary>
public static class JsonElementHelp
{
    /// <summary>
    /// 功能作用：取对象上的属性。父节点不是对象或没有该名则为 false。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名（区分大小写）。</param>
    /// <param name="value">命中的节点。</param>
    /// <returns>取到属性则为 true。</returns>
    public static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        return parent.TryGetProperty(name, out value);
    }

    /// <summary>
    /// 功能作用：取对象属性且类型为 object。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <param name="value">对象节点。</param>
    /// <returns>命中且为 object 则为 true。</returns>
    public static bool TryGetObject(JsonElement parent, string name, out JsonElement value) =>
        TryGetKind(parent, name, JsonValueKind.Object, out value);

    /// <summary>
    /// 功能作用：取对象属性且类型为 array。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <param name="value">数组节点。</param>
    /// <returns>命中且为 array 则为 true。</returns>
    public static bool TryGetArray(JsonElement parent, string name, out JsonElement value) =>
        TryGetKind(parent, name, JsonValueKind.Array, out value);

    /// <summary>
    /// 功能作用：取字符串属性。不是字符串则 null（含缺失）。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>字符串；缺失或类型不对则为 null。</returns>
    public static string? GetString(JsonElement parent, string name) =>
        TryGetKind(parent, name, JsonValueKind.String, out var node) ? node.GetString() : null;

    /// <summary>
    /// 功能作用：取字符串属性并 Trim；空白则视为没有。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>非空字符串；否则 null。</returns>
    public static string? GetNonEmptyString(JsonElement parent, string name)
    {
        var text = GetString(parent, name)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 功能作用：取字符串数组属性；跳过非字符串与空白项。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>字符串列表；缺失则为空列表。</returns>
    public static IReadOnlyList<string> GetStringList(JsonElement parent, string name)
    {
        if (!TryGetArray(parent, name, out var arr))
            return [];

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                list.Add(text);
        }

        return list;
    }

    /// <summary>
    /// 功能作用：取 32 位整数属性。超出 int 范围时钳到 <see cref="int.MinValue"/> / <see cref="int.MaxValue"/>。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>整数；缺失或不是数字则为 null。</returns>
    public static int? GetInt32(JsonElement parent, string name) =>
        TryGetProperty(parent, name, out var node) ? GetInt32(node) : null;

    /// <summary>
    /// 功能作用：把数字节点收成 32 位整数。超出 int 范围时钳到边界。
    /// </summary>
    /// <param name="node">任意 JSON 节点。</param>
    /// <returns>整数；不是数字则为 null。</returns>
    public static int? GetInt32(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Number)
            return null;
        if (node.TryGetInt32(out var i))
            return i;
        if (node.TryGetInt64(out var l))
        {
            if (l > int.MaxValue)
                return int.MaxValue;
            if (l < int.MinValue)
                return int.MinValue;
            return (int)l;
        }

        return null;
    }

    /// <summary>
    /// 功能作用：取正整数。数字节点走 <see cref="GetInt32(JsonElement)"/>，字符串则尝试解析。
    /// </summary>
    /// <param name="node">任意 JSON 节点。</param>
    /// <returns>大于 0 的整数；否则 null。</returns>
    public static int? GetPositiveInt(JsonElement node)
    {
        if (GetInt32(node) is int n && n > 0)
            return n;
        if (node.ValueKind == JsonValueKind.String &&
            int.TryParse(node.GetString(), out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// 功能作用：取对象上的正整数属性。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>大于 0 的整数；缺失或不是正整数则为 null。</returns>
    public static int? GetPositiveInt(JsonElement parent, string name) =>
        TryGetProperty(parent, name, out var node) ? GetPositiveInt(node) : null;

    /// <summary>
    /// 功能作用：取双精度属性。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>数字；缺失或不是数字则为 null。</returns>
    public static double? GetDouble(JsonElement parent, string name)
    {
        if (!TryGetProperty(parent, name, out var node) || node.ValueKind != JsonValueKind.Number)
            return null;
        return node.TryGetDouble(out var value) ? value : null;
    }

    /// <summary>
    /// 功能作用：取布尔属性。只认 JSON true/false，不解析字符串或 0/1。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>布尔；缺失或类型不对则为 null。</returns>
    public static bool? GetBool(JsonElement parent, string name)
    {
        if (!TryGetProperty(parent, name, out var node))
            return null;
        if (node.ValueKind == JsonValueKind.True)
            return true;
        if (node.ValueKind == JsonValueKind.False)
            return false;
        return null;
    }

    /// <summary>
    /// 功能作用：节点本身是字符串则取值；否则 null。不 Trim。
    /// </summary>
    /// <param name="node">任意 JSON 节点。</param>
    /// <returns>字符串；类型不对则为 null。</returns>
    public static string? GetString(JsonElement node) =>
        node.ValueKind == JsonValueKind.String ? node.GetString() : null;

    /// <summary>
    /// 功能作用：节点本身是字符串则 Trim；空白视为没有。
    /// </summary>
    /// <param name="node">任意 JSON 节点。</param>
    /// <returns>非空字符串；否则 null。</returns>
    public static string? GetNonEmptyString(JsonElement node)
    {
        var text = GetString(node)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 功能作用：取日期时间属性。只认字符串并 <see cref="DateTimeOffset.TryParse(string, out DateTimeOffset)"/>。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <returns>解析成功的时间；缺失、非字符串或解析失败则为 null。</returns>
    public static DateTimeOffset? GetDateTimeOffset(JsonElement parent, string name)
    {
        var text = GetString(parent, name);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTimeOffset.TryParse(text, out var dto) ? dto : null;
    }

    /// <summary>
    /// 功能作用：字符串数组属性是否含指定值。
    /// </summary>
    /// <param name="parent">期望为 JSON 对象。</param>
    /// <param name="name">属性名。</param>
    /// <param name="value">要找的字符串。</param>
    /// <param name="comparison">比较方式，默认忽略大小写。</param>
    /// <returns>数组中有匹配项则为 true。</returns>
    public static bool ArrayContainsString(
        JsonElement parent,
        string name,
        string value,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (!TryGetArray(parent, name, out var arr))
            return false;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), value, comparison))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 功能作用：解析 JSON 文本。空白或非法则失败，不抛。
    /// </summary>
    /// <param name="json">JSON 文本。</param>
    /// <param name="document">成功时的文档，调用方负责 Dispose。</param>
    /// <returns>解析成功则为 true。</returns>
    public static bool TryParse(string? json, [NotNullWhen(true)] out JsonDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetKind(JsonElement parent, string name, JsonValueKind kind, out JsonElement value)
    {
        if (!TryGetProperty(parent, name, out value) || value.ValueKind != kind)
        {
            value = default;
            return false;
        }

        return true;
    }
}
