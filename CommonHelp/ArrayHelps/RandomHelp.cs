using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：UUID 批量生成结果，每行一个 UUID 文本。
/// </summary>
public sealed record UuidGenerateResult(string Output);

/// <summary>
/// 功能职责：从序列随机抽取元素、批量生成 UUID/GUID。不负责加密安全随机、不持久化。
/// 方法群组：随机抽取、批量生成 UUID。
/// </summary>
public static class RandomHelp
{
    /// <summary>
    /// 功能作用：从序列随机抽取 count 个元素（拷贝后洗牌截断，不改原序列）。
    /// </summary>
    /// <param name="list">源序列。</param>
    /// <param name="count">抽取个数；自动钳制到 [0, n]。</param>
    /// <returns>新列表，长度为钳制后的 count。</returns>
    public static List<T> GetRandomList<T>(IEnumerable<T> list, int count)
    {
        List<T> result = new List<T>(list); // 拷贝一份防止修改原列表
        int n = result.Count;
        count = Math.Clamp(count, 0, n);

        for (int i = 0; i < count; i++)
        {
            int r = i + Random.Shared.Next(n - i);
            (result[i], result[r]) = (result[r], result[i]);
        }

        result.RemoveRange(count, n - count);
        return result;
    }

    /// <summary>
    /// 功能作用：批量生成 UUID/GUID 字符串，每行一个。
    /// </summary>
    /// <param name="count">生成个数。</param>
    /// <param name="includeHyphens">true 用带连字符格式，false 用 N 格式。</param>
    /// <param name="upperCase">是否转大写。</param>
    /// <returns>成功时 Value.Output 为多行 UUID 文本。</returns>
    public static Result<UuidGenerateResult> GenerateUuids(int count, bool includeHyphens, bool upperCase)
    {
        if (count <= 0)
            return Result<UuidGenerateResult>.Ok(new UuidGenerateResult(string.Empty));

        var sb = new StringBuilder(count * 38);
        var format = includeHyphens ? "D" : "N";
        for (var i = 0; i < count; i++)
        {
            var uuidString = Guid.NewGuid().ToString(format);
            if (upperCase)
                uuidString = uuidString.ToUpperInvariant();

            sb.AppendLine(uuidString);
        }

        return Result<UuidGenerateResult>.Ok(new UuidGenerateResult(sb.ToString()));
    }
}
