using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CommonHelp;

/// <summary>
/// 功能职责：System.Text.Json 序列化/反序列化扩展与预置 Options。不限制泛型约束；不负责业务 DTO 定义。
/// 方法群组：序列化为字符串/UTF-8 字节、反序列化。
/// </summary>
public static class JsonExtensions
{
    /// <summary>属性名全小写的命名策略单例（只读，可在外部构建自定义 options 时复用）。</summary>
    public static readonly LowerCaseNamingPolicy LowerCaseNamingPolicyInstance = new();

    /// <summary>缩进 + 属性名全小写 + 非 ASCII 不转义（缓存实例，避免每次调用重建元数据）。</summary>
    public static readonly JsonSerializerOptions IndentedLowerCaseOptions = new()
    {
        PropertyNamingPolicy = LowerCaseNamingPolicyInstance, // 关键：使用自定义命名策略（全小写操作）
        WriteIndented = true, // 让 JSON 格式化
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：禁用不必要的转义
    };

    /// <summary>缩进 + 保留原属性名 + 非 ASCII 不转义（缓存实例，避免每次调用重建元数据）。</summary>
    public static readonly JsonSerializerOptions IndentedRelaxedOptions = new()
    {
        WriteIndented = true, // 让 JSON 格式化
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：禁用不必要的转义
    };

    /// <summary>容错反序列化选项：忽略属性名大小写并容忍尾逗号。</summary>
    public static readonly JsonSerializerOptions LenientDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true, // 忽略字段大小写（容错）
        AllowTrailingCommas = true //允许 JSON 结尾有逗号
    };

    /// <summary>紧凑（不缩进）+ 保留原属性名 + 非 ASCII 不转义。适合 localStorage / SQLite / HTTP 请求体。</summary>
    public static readonly JsonSerializerOptions CompactRelaxedOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };



    /// <summary>
    /// 功能作用：将 <see cref="JsonElement"/> 序列化为 JSON 文本（WriteTo，AOT 安全）。
    /// </summary>
    /// <param name="element">已解析的 JSON 节点。</param>
    /// <param name="options">序列化选项（缩进与转义）。</param>
    /// <returns>JSON 文本。</returns>
    public static string JsonSe(this JsonElement element, JsonSerializerOptions options)
    {
        var writerOptions = new JsonWriterOptions
        {
            Indented = options.WriteIndented,
            Encoder = options.Encoder,
        };
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// 功能作用：使用源生成 <see cref="JsonTypeInfo{T}"/> 序列化（AOT / Trim 安全）。
    /// </summary>
    /// <param name="obj">任意对象。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <returns>JSON 文本。</returns>
    public static string JsonSe<T>(this T obj, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Serialize(obj, typeInfo);

    /// <summary>
    /// 功能作用：使用源生成 <see cref="JsonTypeInfo{T}"/> 反序列化（AOT / Trim 安全）。
    /// </summary>
    /// <param name="json">JSON 文本。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <returns>对象；解析失败可能为 null。</returns>
    public static T? JsonDe<T>(this string json, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(json, typeInfo);

    /// <summary>
    /// 功能作用：使用源生成 <see cref="JsonTypeInfo{T}"/> 序列化为 UTF-8 字节（AOT / Trim 安全）。
    /// </summary>
    /// <param name="obj">任意对象。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <returns>UTF-8 JSON 字节。</returns>
    public static byte[] JsonSeUtf8<T>(this T obj, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToUtf8Bytes(obj, typeInfo);

    /// <summary>
    /// 功能作用：使用指定 Options 序列化对象。
    /// </summary>
    /// <param name="obj">任意对象。</param>
    /// <param name="options">序列化选项。</param>
    /// <returns>JSON 文本。</returns>
    [RequiresDynamicCode("JSON 序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static string JsonSe<T>(this T obj, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(obj, options);

    /// <summary>
    /// 功能作用：使用指定 Options 反序列化 JSON。
    /// </summary>
    /// <param name="json">JSON 文本。</param>
    /// <param name="options">反序列化选项。</param>
    /// <returns>对象；解析失败可能为 null。</returns>
    [RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static T? JsonDe<T>(this string json, JsonSerializerOptions options) where T : class =>
        JsonSerializer.Deserialize<T>(json, options);

    /// <summary>
    /// 功能作用：使用 JsonSerializer 默认 Options 序列化。
    /// </summary>
    /// <param name="obj">任意对象。</param>
    /// <returns>JSON 文本。</returns>
    [RequiresDynamicCode("JSON 序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static string JsonSe<T>(this T obj) =>
        JsonSerializer.Serialize(obj);

    /// <summary>
    /// 功能作用：使用默认 Options 反序列化。
    /// </summary>
    /// <param name="json">JSON 文本。</param>
    /// <returns>对象；解析失败可能为 null。</returns>
    [RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static T? JsonDe<T>(this string json) where T : class =>
        JsonSerializer.Deserialize<T>(json);

    /// <summary>
    /// 功能作用：序列化为 UTF-8 JSON 字节（HTTP 请求体等）；Options 决定缩进与转义。
    /// </summary>
    /// <param name="obj">任意对象。</param>
    /// <param name="options">序列化选项。</param>
    /// <returns>UTF-8 JSON 字节。</returns>
    [RequiresDynamicCode("JSON 序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static byte[] JsonSeUtf8<T>(this T obj, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(obj, options);

    /// <summary>
    /// 功能职责：JSON 属性名全小写命名策略。
    /// 方法群组：转换属性名。
    /// </summary>
    public class LowerCaseNamingPolicy : JsonNamingPolicy
    {
        /// <summary>
        /// 功能作用：把属性名转为全小写。
        /// </summary>
        /// <param name="name">原属性名。</param>
        /// <returns>全小写名称。</returns>
        public override string ConvertName(string name) => name.ToLowerInvariant();
    }
}

/// <summary>
/// 功能职责：容错 bool JSON 转换。接受 true/false、"true"/"false"、"1"/"0"、1/0。不负责其他类型。
/// 方法群组：读取、写出。
/// </summary>
public class FlexibleBoolConverter : JsonConverter<bool>
{
    /// <summary>
    /// 功能作用：读取 JSON 值并容错转为 bool；无法识别时返回 false。
    /// </summary>
    /// <param name="reader">JSON 读取器。</param>
    /// <param name="typeToConvert">目标类型。</param>
    /// <param name="options">序列化选项。</param>
    /// <returns>布尔值。</returns>
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 如果 JSON 值是布尔类型 (true/false)
        if (reader.TokenType == JsonTokenType.True) return true;
        if (reader.TokenType == JsonTokenType.False) return false;

        // 如果 JSON 值是字符串 ("true"/"false")
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            // 处理可能的数字字符串，如 "1" 为 true
            if (value == "1") return true;
            if (value == "0") return false;
        }

        // 如果是数字类型 (1/0)
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var l))
                return l != 0;
            if (reader.TryGetDouble(out var d))
                return d != 0;
        }

        return false; // 默认值
    }

    /// <summary>
    /// 功能作用：写出标准 JSON 布尔值。
    /// </summary>
    /// <param name="writer">JSON 写入器。</param>
    /// <param name="value">布尔值。</param>
    /// <param name="options">序列化选项。</param>
    /// <returns>无返回值；写入 writer。</returns>
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
