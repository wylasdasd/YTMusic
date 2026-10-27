using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：按位宽归一化后的位运算结果（十进制值）。
/// </summary>
public sealed record BitwiseComputeResult(long Result);

/// <summary>
/// 功能职责：在 1–64 位宽下做 AND/OR/XOR/NOT/SHL/SHR_A/SHR_L，并处理掩码截断与符号扩展。不负责 UI 绑定。
/// 方法群组：运算、移位、位宽归一化、格式化展示。
/// </summary>
public static class BitwiseHelp
{
    /// <summary>
    /// 功能作用：判断操作名是否为移位（SHL/SHR_A/SHR_L）。
    /// </summary>
    /// <param name="op">操作名。</param>
    /// <returns>是移位操作为 true。</returns>
    public static bool IsShiftOp(string op) => op is "SHL" or "SHR_A" or "SHR_L";

    /// <summary>
    /// 功能作用：取指定位宽的低位全 1 掩码；64 位返回 -1L。
    /// </summary>
    /// <param name="bitWidth">位宽。</param>
    /// <returns>掩码。</returns>
    public static long GetMask(int bitWidth) =>
        bitWidth == 64 ? -1L : (1L << bitWidth) - 1;

    /// <summary>
    /// 功能作用：把截断在 bitWidth 位内的值按最高位做符号扩展。
    /// </summary>
    /// <param name="value">已截断的无符号值。</param>
    /// <param name="bitWidth">原始位宽。</param>
    /// <returns>符号扩展后的 long。</returns>
    public static long SignExtend(long value, int bitWidth)
    {
        if (bitWidth == 64) return value;

        var mask = GetMask(bitWidth);
        var signBit = 1L << (bitWidth - 1);
        if ((value & signBit) != 0)
            return value | ~mask;

        return value & mask;
    }

    /// <summary>
    /// 功能作用：把结果裁剪到位宽内；有符号时可选符号扩展。
    /// </summary>
    /// <param name="res">原始结果。</param>
    /// <param name="bitWidth">目标位宽。</param>
    /// <param name="isSigned">是否按有符号解释。</param>
    /// <param name="signExtendWhenSigned">有符号时是否做符号扩展。</param>
    /// <returns>归一化后的值。</returns>
    public static long NormalizeResult(long res, int bitWidth, bool isSigned, bool signExtendWhenSigned = true)
    {
        res &= GetMask(bitWidth);
        if (isSigned && signExtendWhenSigned)
            res = SignExtend(res, bitWidth);
        return res;
    }

    /// <summary>
    /// 功能作用：执行一次位运算（AND/OR/XOR/NOT 或移位）并按位宽归一化；NOT 只取操作数 A。
    /// </summary>
    /// <param name="operation">操作名（AND/OR/XOR/NOT/SHL/SHR_A/SHR_L）。</param>
    /// <param name="operandA">操作数 A。</param>
    /// <param name="operandB">操作数 B（移位时不用）。</param>
    /// <param name="operandC">移位量（非移位时不用）。</param>
    /// <param name="bitWidth">位宽 1–64。</param>
    /// <param name="isSigned">是否有符号。</param>
    /// <param name="currentResult">当前结果；移位可从它取被移位数。</param>
    /// <param name="shiftFromResult">true 时移位以 currentResult 为输入。</param>
    /// <returns>成功时携带归一化结果；异常时 Fail。</returns>
    public static Result<BitwiseComputeResult> Compute(
        string operation,
        long operandA,
        long operandB,
        int operandC,
        int bitWidth,
        bool isSigned,
        long currentResult,
        bool shiftFromResult)
    {
        try
        {
            if (IsShiftOp(operation))
            {
                var res = ApplyShift(operation, operandA, currentResult, operandC, bitWidth, isSigned, shiftFromResult);
                return Result<BitwiseComputeResult>.Ok(new BitwiseComputeResult(res));
            }

            var mask = GetMask(bitWidth);
            var am = operandA & mask;
            var bm = operandB & mask;
            if (isSigned)
            {
                am = SignExtend(am, bitWidth);
                bm = SignExtend(bm, bitWidth);
            }

            var result = operation switch
            {
                "AND" => am & bm,
                "OR" => am | bm,
                "XOR" => am ^ bm,
                "NOT" => ~(am & mask),
                _ => 0L,
            };

            return Result<BitwiseComputeResult>.Ok(new BitwiseComputeResult(NormalizeResult(result, bitWidth, isSigned)));
        }
        catch (Exception ex)
        {
            return Result<BitwiseComputeResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 功能作用：执行移位；被移位数可选操作数 A 或当前结果，移位量钳制在 0–bitWidth。
    /// </summary>
    /// <param name="operation">SHL / SHR_A / SHR_L。</param>
    /// <param name="operandA">操作数 A。</param>
    /// <param name="currentResult">当前结果。</param>
    /// <param name="operandC">移位量。</param>
    /// <param name="bitWidth">位宽。</param>
    /// <param name="isSigned">是否有符号（影响算术右移）。</param>
    /// <param name="fromResult">true 时以 currentResult 为被移位数。</param>
    /// <returns>归一化后的移位结果。</returns>
    public static long ApplyShift(
        string operation,
        long operandA,
        long currentResult,
        int operandC,
        int bitWidth,
        bool isSigned,
        bool fromResult)
    {
        var mask = GetMask(bitWidth);
        var shiftAmount = Math.Clamp(operandC, 0, bitWidth);
        var input = fromResult ? currentResult & mask : operandA & mask;

        var res = operation switch
        {
            "SHL" => ShiftLeft(input, shiftAmount, bitWidth, mask),
            "SHR_A" => isSigned
                ? ShiftRightArithmetic(input, shiftAmount, bitWidth, mask)
                : ShiftRightLogical(input, shiftAmount, bitWidth, mask),
            "SHR_L" => ShiftRightLogical(input, shiftAmount, bitWidth, mask),
            _ => input,
        };

        if (isSigned && operation is "SHR_A" or "SHL")
            return NormalizeResult(res, bitWidth, isSigned);
        return NormalizeResult(res, bitWidth, isSigned, signExtendWhenSigned: false);
    }

    /// <summary>
    /// 功能作用：按有/无符号把位宽内的值格式化为十进制字符串。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="bitWidth">位宽。</param>
    /// <param name="isSigned">是否按有符号展示。</param>
    /// <returns>十进制文本。</returns>
    public static string FormatValue(long value, int bitWidth, bool isSigned)
    {
        var maskedValue = value & GetMask(bitWidth);

        if (isSigned)
            return SignExtend(maskedValue, bitWidth).ToString();

        return ((ulong)maskedValue).ToString();
    }

    /// <summary>
    /// 功能作用：格式化为二进制，左侧补零到目标位宽并每 4 位分组。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="bitWidth">位宽。</param>
    /// <returns>分组后的二进制文本。</returns>
    public static string FormatBinary(long value, int bitWidth)
    {
        if (bitWidth <= 0)
            return string.Empty;

        var maskedValue = (ulong)(value & GetMask(bitWidth));
        var sb = new StringBuilder(bitWidth + (bitWidth - 1) / 4);
        for (var i = 0; i < bitWidth; i++)
        {
            if (i > 0 && (bitWidth - i) % 4 == 0)
                sb.Append(' ');

            var bit = (maskedValue >> (bitWidth - 1 - i)) & 1UL;
            sb.Append(bit != 0 ? '1' : '0');
        }

        return sb.ToString();
    }

    /// <summary>
    /// 功能作用：按位宽截断后格式化为大写十六进制。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="bitWidth">位宽。</param>
    /// <returns>十六进制文本。</returns>
    public static string FormatHex(long value, int bitWidth) =>
        (value & GetMask(bitWidth)).ToString("X");

    /// <summary>
    /// 左移并在位宽内截断（超出位宽的结果为 0）。
    /// </summary>
    private static long ShiftLeft(long value, int shift, int bitWidth, long mask)
    {
        if (shift >= bitWidth) return 0;
        return (value & mask) << shift & mask;
    }

    /// <summary>
    /// 逻辑右移（高位补 0）。
    /// </summary>
    private static long ShiftRightLogical(long value, int shift, int bitWidth, long mask)
    {
        if (shift >= bitWidth) return 0;
        return (long)((ulong)(value & mask) >> shift);
    }

    /// <summary>
    /// 算术右移（高位补符号位），正确处理 1–64 位宽边界。
    /// </summary>
    private static long ShiftRightArithmetic(long value, int shift, int bitWidth, long mask)
    {
        if (shift >= bitWidth)
        {
            var msb = 1L << (bitWidth - 1);
            return (value & msb) != 0 ? mask : 0;
        }

        var bits = value & mask;
        var msbBit = 1L << (bitWidth - 1);
        ulong u = (ulong)bits >> shift;
        if ((bits & msbBit) != 0)
        {
            var uMask = bitWidth == 64 ? ulong.MaxValue : (ulong)mask;
            u |= uMask ^ (uMask >> shift);
        }

        return bitWidth == 64 ? (long)u : (long)(u & (ulong)mask);
    }
}
