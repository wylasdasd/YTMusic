using System.Globalization;

namespace CommonHelp;

/// <summary>
/// 功能职责：颜色状态快照（Hex 数字、RGBA 0–255、HSL：H 0–360，S/L 0–100）。
/// </summary>
public sealed record ColorState(
    string HexDigits,
    int R,
    int G,
    int B,
    int A,
    double H,
    double S,
    double L)
{
    /// <summary>带 # 前缀的展示用 Hex（Alpha 不透明时省略 Alpha 位）。</summary>
    public string HexDisplay => A >= 255 ? $"#{HexDigits[..6]}" : $"#{HexDigits}";
    /// <summary>CSS rgba() 字符串。</summary>
    public string RgbaCss =>
        $"rgba({R}, {G}, {B}, {(A / 255.0).ToString("0.##", CultureInfo.InvariantCulture)})";
    /// <summary>Alpha 的百分比表示（0–100）。</summary>
    public int AlphaPercent => (int)Math.Round(A * 100.0 / 255);
}

/// <summary>
/// 功能职责：HEX ↔ RGBA ↔ HSL 互转。不负责颜色选择 UI。
/// 方法群组：规范化 Hex、从 Hex/RGBA 构建、分量钳制。
/// </summary>
public static class ColorConvertHelp
{
    /// <summary>
    /// 功能作用：规范化 Hex 输入（去 #、截断到最多 8 位、转大写）。
    /// </summary>
    /// <param name="value">用户输入的 Hex。</param>
    /// <returns>规范化后的数字串；空白返回空串。</returns>
    public static string NormalizeHexDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var hex = value.Trim().TrimStart('#');
        return (hex.Length > 8 ? hex[..8] : hex).ToUpperInvariant();
    }

    /// <summary>
    /// 功能作用：解析 6 位 RGB 或 8 位 RGBA Hex；长度/格式非法时 Fail。
    /// </summary>
    /// <param name="hexDigits">已规范化的 Hex 数字（不含 #）。</param>
    /// <returns>成功时携带含 HSL 的颜色状态。</returns>
    public static Result<ColorState> FromHex(string hexDigits)
    {
        try
        {
            var hex = hexDigits;
            if (hex.Length is not (6 or 8))
                return Result<ColorState>.Fail("invalid length");

            var r = ParseHexByte(hex, 0);
            var g = ParseHexByte(hex, 2);
            var b = ParseHexByte(hex, 4);
            var a = hex.Length == 8 ? ParseHexByte(hex, 6) : 255;
            return Result<ColorState>.Ok(BuildFromRgba(r, g, b, a));
        }
        catch
        {
            return Result<ColorState>.Fail("invalid hex");
        }
    }

    /// <summary>
    /// 功能作用：从 RGBA 分量构建颜色状态；各分量钳制到 0–255。
    /// </summary>
    /// <param name="r">红 0–255。</param>
    /// <param name="g">绿 0–255。</param>
    /// <param name="b">蓝 0–255。</param>
    /// <param name="a">透明 0–255。</param>
    /// <returns>含 Hex 与 HSL 的颜色状态。</returns>
    public static ColorState FromRgba(int r, int g, int b, int a) =>
        BuildFromRgba(ClampByte(r), ClampByte(g), ClampByte(b), ClampByte(a));

    /// <summary>
    /// 功能作用：把值裁剪到 0–255。
    /// </summary>
    /// <param name="value">任意整数。</param>
    /// <returns>钳制后的字节范围整数。</returns>
    public static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    /// <summary>
    /// 功能作用：透明度百分比（0–100）转 Alpha 字节（0–255）。
    /// </summary>
    /// <param name="alphaPercent">百分比。</param>
    /// <returns>Alpha 字节值。</returns>
    public static int AlphaPercentToByte(int alphaPercent) =>
        (int)Math.Round(Math.Clamp(alphaPercent, 0, 100) * 255.0 / 100);

    /// <summary>
    /// 由 RGBA 分量构造 Hex 数字与 HSL，组装成颜色状态。
    /// </summary>
    private static ColorState BuildFromRgba(int r, int g, int b, int a)
    {
        var hexDigits = a >= 255
            ? $"{r:X2}{g:X2}{b:X2}"
            : $"{r:X2}{g:X2}{b:X2}{a:X2}";

        ComputeHsl(r, g, b, out var h, out var s, out var l);
        return new ColorState(hexDigits, r, g, b, a, h, s, l);
    }

    /// <summary>
    /// RGB 转 HSL（H 0–360，S/L 0–100）。
    /// </summary>
    private static void ComputeHsl(int r, int g, int b, out double h, out double s, out double l)
    {
        var rd = r / 255.0;
        var gd = g / 255.0;
        var bd = b / 255.0;

        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));

        l = (max + min) / 2.0;

        if (max == min)
        {
            h = s = 0;
        }
        else
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == rd)
                h = (gd - bd) / d + (gd < bd ? 6 : 0);
            else if (max == gd)
                h = (bd - rd) / d + 2;
            else
                h = (rd - gd) / d + 4;

            h /= 6;
        }

        h *= 360;
        s *= 100;
        l *= 100;
    }

    /// <summary>
    /// 解析 hex 字符串中从 start 开始的两个字符为一个字节。
    /// </summary>
    private static int ParseHexByte(string hex, int start) =>
        int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
