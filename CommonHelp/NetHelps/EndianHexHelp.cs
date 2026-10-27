using System.Globalization;

namespace CommonHelp;

/// <summary>
/// 功能职责：数据类型定义（Id、展示标签、字节宽度、是否浮点）。
/// </summary>
public sealed record EndianHexDataType(string Id, string Label, int ByteSize, bool IsFloat);

/// <summary>
/// 功能职责：端序换算结果（空格分隔 Hex、每字节 8 位二进制、十进制）。
/// </summary>
public sealed record EndianHexResult(string HexOutput, string BinaryOutput, string DecimalOutput);

/// <summary>
/// 功能职责：数值与十六进制字节互转。支持 int8–uint64、float32/float64 与大端/小端。不负责文件 I/O。
/// 方法群组：数值转 Hex、Hex 转数值。
/// </summary>
public static class EndianHexHelp
{
    /// <summary>全部支持的数据类型；未知类型 Id 回退到 int32。</summary>
    public static IReadOnlyList<EndianHexDataType> DataTypes { get; } =
    [
        new("int8", "int8 / sbyte", 1, false),
        new("uint8", "uint8 / byte", 1, false),
        new("int16", "int16", 2, false),
        new("uint16", "uint16", 2, false),
        new("int32", "int32", 4, false),
        new("uint32", "uint32", 4, false),
        new("int64", "int64", 8, false),
        new("uint64", "uint64", 8, false),
        new("float32", "float32", 4, true),
        new("float64", "float64", 8, true),
    ];

    /// <summary>
    /// 功能作用：数值转 Hex 字节；inputIsHex 为 true 时直接解析 Hex。同时输出二进制与按目标类型解码的十进制。
    /// </summary>
    /// <param name="input">十进制或 Hex 文本。</param>
    /// <param name="dataTypeId">类型 Id；未知回退 int32。</param>
    /// <param name="bigEndian">是否大端。</param>
    /// <param name="inputIsHex">输入是否已是 Hex。</param>
    /// <returns>成功时携带三种展示；解析失败则 Fail。</returns>
    public static Result<EndianHexResult> ConvertValueToHex(
        string? input, string? dataTypeId, bool bigEndian, bool inputIsHex)
    {
        var type = ResolveDataType(dataTypeId);
        try
        {
            var bytes = inputIsHex
                ? EncodingHelp.ParseHexBytes(input ?? string.Empty, type.ByteSize)
                : EncodeValue(input ?? string.Empty, type, bigEndian);

            return Result<EndianHexResult>.Ok(BuildResult(bytes, type, bigEndian));
        }
        catch (Exception ex)
        {
            return Result<EndianHexResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 功能作用：Hex 字节转十进制；按目标类型与端序解码。
    /// </summary>
    /// <param name="input">Hex 文本。</param>
    /// <param name="dataTypeId">类型 Id；未知回退 int32。</param>
    /// <param name="bigEndian">是否大端。</param>
    /// <returns>成功时携带三种展示；解析失败则 Fail。</returns>
    public static Result<EndianHexResult> ConvertHexToValue(string? input, string? dataTypeId, bool bigEndian)
    {
        var type = ResolveDataType(dataTypeId);
        try
        {
            var bytes = EncodingHelp.ParseHexBytes(input ?? string.Empty, type.ByteSize);
            return Result<EndianHexResult>.Ok(BuildResult(bytes, type, bigEndian));
        }
        catch (Exception ex)
        {
            return Result<EndianHexResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 按类型 Id 查找类型定义；未知 Id 回退到 int32。
    /// </summary>
    private static EndianHexDataType ResolveDataType(string? dataTypeId) =>
        DataTypes.FirstOrDefault(t => t.Id == dataTypeId) ?? DataTypes[4];

    /// <summary>
    /// 组装 Hex、二进制与十进制三种展示。
    /// </summary>
    private static EndianHexResult BuildResult(byte[] bytes, EndianHexDataType type, bool bigEndian) =>
        new(
            EncodingHelp.FormatHexBytes(bytes),
            FormatBytesBinary(bytes),
            DecodeBytes(bytes, type, bigEndian));

    private static string FormatBytesBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        return string.Create(bytes.Length * 9 - 1, bytes, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                if (i > 0)
                    span[i * 9 - 1] = ' ';

                var b = src[i];
                var baseIdx = i * 9;
                for (var bit = 0; bit < 8; bit++)
                {
                    span[baseIdx + bit] = ((b >> (7 - bit)) & 1) != 0 ? '1' : '0';
                }
            }
        });
    }

    /// <summary>
    /// 十进制字符串按目标类型编码为字节序列（按端序排列）。
    /// </summary>
    private static byte[] EncodeValue(string text, EndianHexDataType type, bool bigEndian)
    {
        text = text.Trim();
        return type.Id switch
        {
            "int8" => [(byte)sbyte.Parse(text, CultureInfo.InvariantCulture)],
            "uint8" => [byte.Parse(text, CultureInfo.InvariantCulture)],
            "int16" => ToOrderedBytes(BitConverter.GetBytes(short.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "uint16" => ToOrderedBytes(BitConverter.GetBytes(ushort.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "int32" => ToOrderedBytes(BitConverter.GetBytes(int.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "uint32" => ToOrderedBytes(BitConverter.GetBytes(uint.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "int64" => ToOrderedBytes(BitConverter.GetBytes(long.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "uint64" => ToOrderedBytes(BitConverter.GetBytes(ulong.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "float32" => ToOrderedBytes(BitConverter.GetBytes(float.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            "float64" => ToOrderedBytes(BitConverter.GetBytes(double.Parse(text, CultureInfo.InvariantCulture)), bigEndian),
            _ => throw new InvalidOperationException("未知类型。"),
        };
    }

    /// <summary>
    /// 字节序列按目标类型与端序解码为十进制字符串（浮点用往返格式 "R"）。
    /// </summary>
    private static string DecodeBytes(byte[] bytes, EndianHexDataType type, bool bigEndian)
    {
        var ordered = Order(bytes, bigEndian);
        return type.Id switch
        {
            "int8" => ((sbyte)bytes[0]).ToString(CultureInfo.InvariantCulture),
            "uint8" => bytes[0].ToString(CultureInfo.InvariantCulture),
            "int16" => BitConverter.ToInt16(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "uint16" => BitConverter.ToUInt16(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "int32" => BitConverter.ToInt32(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "uint32" => BitConverter.ToUInt32(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "int64" => BitConverter.ToInt64(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "uint64" => BitConverter.ToUInt64(ordered, 0).ToString(CultureInfo.InvariantCulture),
            "float32" => BitConverter.ToSingle(ordered, 0).ToString("R", CultureInfo.InvariantCulture),
            "float64" => BitConverter.ToDouble(ordered, 0).ToString("R", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("未知类型。"),
        };
    }

    /// <summary>
    /// 按目标端序重排字节：与本机端序不一致时反转副本。
    /// </summary>
    private static byte[] Order(byte[] bytes, bool bigEndian)
    {
        if (bigEndian == BitConverter.IsLittleEndian)
        {
            var copy = (byte[])bytes.Clone();
            Array.Reverse(copy);
            return copy;
        }
        return bytes;
    }

    /// <summary>按目标端序原地规范化新生成的字节数组。</summary>
    private static byte[] ToOrderedBytes(byte[] bytes, bool bigEndian)
    {
        if (bigEndian == BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }
}
