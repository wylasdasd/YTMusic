namespace CommonHelp;

/// <summary>
/// 功能职责：扩展名推断 MIME、Base64 图片魔数检测、Base64 校验与 Data URI（RFC 2397）解析/构建。不负责读文件内容。
/// 方法群组：推断 MIME、校验/解码 Base64、解析/构建 Data URI。
/// </summary>
public static class MimeHelp
{
    /// <summary>
    /// 功能作用：按扩展名推断常见 MIME；未知返回 text/plain。
    /// </summary>
    /// <param name="path">文件路径或文件名。</param>
    /// <returns>MIME 字符串。</returns>
    public static string GuessContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".md" => "text/markdown",
            _ => "text/plain"
        };

    /// <summary>
    /// 功能作用：按文件名或已有 Content-Type 推断图片 MIME。contentType 以 image/ 开头时原样返回，否则按扩展名推断（缺省 image/jpeg）。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <param name="contentType">已有 Content-Type。</param>
    /// <returns>图片 MIME。</returns>
    public static string GuessImageMime(string fileName, string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return contentType;

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// 功能作用：按 Base64 前缀魔数检测图片类型（png/jpeg/gif/svg/bmp/webp）；无法识别返回空串。
    /// </summary>
    /// <param name="base64">Base64 文本。</param>
    /// <returns>图片 MIME；无法识别为空串。</returns>
    public static string DetectImageMimeType(string base64)
    {
        if (base64.StartsWith("iVBORw0KGgo")) return "image/png";
        if (base64.StartsWith("/9j/")) return "image/jpeg";
        if (base64.StartsWith("R0lGODlh")) return "image/gif";
        if (base64.StartsWith("PHN2Zy")) return "image/svg+xml";
        if (base64.StartsWith("Qk0")) return "image/bmp";
        if (base64.StartsWith("UklGR")) return "image/webp";
        return string.Empty;
    }

    /// <summary>
    /// 功能作用：校验字符串是否为合法 Base64；空白返回 false，长度不足时按补齐容忍。
    /// </summary>
    /// <param name="value">待校验文本。</param>
    /// <returns>合法为 true。</returns>
    public static bool IsValidBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // 零分配校验；长度不足时按补齐容忍
        return System.Buffers.Text.Base64.IsValid(value, out _);
    }

    /// <summary>
    /// 功能作用：拆解 <c>data:&lt;mime&gt;;base64,&lt;payload&gt;</c>；非该形式返回 false，输出参数为空串。
    /// </summary>
    /// <param name="input">可能的 Data URI。</param>
    /// <param name="dataUrl">规范化后的 Data URI。</param>
    /// <param name="base64Part">载荷。</param>
    /// <param name="mimeType">MIME；缺省为 image/*。</param>
    /// <returns>解析成功为 true。</returns>
    public static bool TryParseDataUrl(
        string input,
        out string dataUrl,
        out string base64Part,
        out string mimeType)
    {
        dataUrl = string.Empty;
        base64Part = string.Empty;
        mimeType = string.Empty;

        if (!input.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        var commaIndex = input.IndexOf(',');
        if (commaIndex <= 5)
            return false;

        var metadata = input.AsSpan(5, commaIndex - 5);
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            return false;

        var semiIndex = metadata.IndexOf(';');
        var mimeSpan = (semiIndex >= 0 ? metadata[..semiIndex] : metadata).Trim();
        mimeType = mimeSpan.IsEmpty ? "image/*" : mimeSpan.ToString();

        base64Part = input[(commaIndex + 1)..];
        dataUrl = $"data:{mimeType};base64,{base64Part}";
        return true;
    }

    /// <summary>
    /// 功能作用：由字节构建 Data URI。
    /// </summary>
    /// <param name="mimeType">MIME。</param>
    /// <param name="bytes">原始字节。</param>
    /// <returns><c>data:&lt;mime&gt;;base64,&lt;payload&gt;</c>。</returns>
    public static string BuildDataUrl(string mimeType, byte[] bytes) =>
        BuildDataUrl(mimeType, Convert.ToBase64String(bytes));

    /// <summary>
    /// 功能作用：用已有 Base64 字符串构建 Data URI（不再二次编码）。
    /// </summary>
    /// <param name="mimeType">MIME。</param>
    /// <param name="base64">已编码的 Base64。</param>
    /// <returns>Data URI。</returns>
    public static string BuildDataUrl(string mimeType, string base64) =>
        $"data:{mimeType};base64,{base64}";

    /// <summary>
    /// 功能作用：归一化图片 MIME。<c>image/jpg</c> → <c>image/jpeg</c>；裸类型 png/jpeg/jpg/gif/webp/bmp/svg 补 <c>image/</c> 前缀。
    /// </summary>
    /// <param name="mimeOrType">MIME 或裸类型。</param>
    /// <returns>归一化后的 MIME；空白返回空串。</returns>
    public static string NormalizeImageMime(string? mimeOrType)
    {
        if (string.IsNullOrWhiteSpace(mimeOrType))
            return string.Empty;

        var value = mimeOrType.Trim();
        if (value.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("jpg", StringComparison.OrdinalIgnoreCase))
            return "image/jpeg";
        if (value.Equals("png", StringComparison.OrdinalIgnoreCase))
            return "image/png";
        if (value.Equals("gif", StringComparison.OrdinalIgnoreCase))
            return "image/gif";
        if (value.Equals("webp", StringComparison.OrdinalIgnoreCase))
            return "image/webp";
        if (value.Equals("bmp", StringComparison.OrdinalIgnoreCase))
            return "image/bmp";
        if (value.Equals("svg", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("svg+xml", StringComparison.OrdinalIgnoreCase))
            return "image/svg+xml";

        return value.ToLowerInvariant();
    }

    /// <summary>
    /// 功能作用：把 Base64 文本解码为字节；空白或非法时返回 false。
    /// </summary>
    /// <param name="value">Base64 文本。</param>
    /// <param name="bytes">解码字节；失败为空数组。</param>
    /// <returns>解码成功为 true。</returns>
    public static bool TryDecodeBase64(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (!IsValidBase64(trimmed))
            return false;

        bytes = Convert.FromBase64String(trimmed);
        return true;
    }
}
