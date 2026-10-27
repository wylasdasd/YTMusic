namespace CommonHelp;

/// <summary>
/// 功能职责：HTTP 根地址规范化与路径拼接。不发请求、不解析查询串。
/// 方法群组：规范化根、拼 URI。
/// </summary>
public static class UriHelp
{
    /// <summary>
    /// 功能作用：去掉首尾空白和末尾斜杠。空或空白则空串。
    /// </summary>
    /// <param name="url">根地址；可空。</param>
    /// <returns>已规范化的根；空白则为空串。</returns>
    public static string NormalizeRoot(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        return url.Trim().TrimEnd('/');
    }

    /// <summary>
    /// 功能作用：把根地址与相对路径拼成绝对 URI。path 可带或不带前导斜杠。
    /// </summary>
    /// <param name="root">根地址；会先 <see cref="NormalizeRoot"/>。</param>
    /// <param name="path">相对路径，如 <c>api/chat</c> 或 <c>/api/chat</c>。</param>
    /// <returns>拼接后的 URI。</returns>
    public static Uri Combine(string? root, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var relative = path.StartsWith('/') ? path : "/" + path;
        return new Uri(NormalizeRoot(root) + relative);
    }
}
