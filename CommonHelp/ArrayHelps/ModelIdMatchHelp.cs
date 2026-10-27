namespace CommonHelp;

/// <summary>
/// 功能职责：带斜杠键的三段回退匹配——精确 → 去掉最后一段前缀后的尾段 → 候选键以 <c>/key</c> 结尾。不关心键的业务含义。
/// 方法群组：查表（TryMatch）。
/// </summary>
public static class ModelIdMatchHelp
{
    /// <summary>
    /// 功能作用：在候选集合里按「精确匹配 → 尾段匹配 → 后缀匹配」找键，全部 OrdinalIgnoreCase。
    /// 输入：entries 候选集合；keySelector 取候选键；key 查询键（可含一段 <c>/</c> 前缀）；valueSelector 取候选的值。
    /// 输出：命中返回值（值类型包装为可空）；空白键或都不命中返回 null。
    /// </summary>
    public static TValue? TryMatch<TEntry, TValue>(
        IEnumerable<TEntry> entries,
        Func<TEntry, string> keySelector,
        string? key,
        Func<TEntry, TValue> valueSelector)
        where TValue : struct
    {
        var id = key?.Trim();
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (var entry in entries)
        {
            if (keySelector(entry).Equals(id, StringComparison.OrdinalIgnoreCase))
                return valueSelector(entry);
        }

        var slash = id.LastIndexOf('/');
        if (slash >= 0 && slash < id.Length - 1)
        {
            var tail = id[(slash + 1)..];
            foreach (var entry in entries)
            {
                if (keySelector(entry).Equals(tail, StringComparison.OrdinalIgnoreCase))
                    return valueSelector(entry);
            }
        }

        foreach (var entry in entries)
        {
            if (keySelector(entry).EndsWith("/" + id, StringComparison.OrdinalIgnoreCase))
                return valueSelector(entry);
        }

        return null;
    }
}
