using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommonTool.JsonHelps
{
    public static class JsonExtensions
    {
        public static LowerCaseNamingPolicy lowerCaseNamingPolicy = new LowerCaseNamingPolicy();

      /// <summary>
      /// json 小写
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="obj"></param>
      /// <returns></returns>
        public static string JsonSeToLow<T>(this T obj) where T : class
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = lowerCaseNamingPolicy, // 关键：使用自定义命名策略（全小写操作）
                WriteIndented = true, // 让 JSON 格式化
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：禁用不必要的转义
            };
            // 使用 JsonSerializer.Serialize 方法进行序列化
            return JsonSerializer.Serialize(obj, options);
        }

        /// <summary>
        /// 原样json
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string JsonSeNoLow<T>(this T obj) where T : class
        {
            var options = new JsonSerializerOptions
            {
                //PropertyNamingPolicy = lowerCaseNamingPolicy, // 关键：使用自定义命名策略（全小写操作）
                WriteIndented = true, // 让 JSON 格式化
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：禁用不必要的转义
            };
            // 使用 JsonSerializer.Serialize 方法进行序列化
            return JsonSerializer.Serialize(obj, options);
        }

        /// <summary>
        /// 忽略大小写
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T? JsonDe<T>(this string json) where T : class
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // 忽略字段大小写（容错）
                AllowTrailingCommas = true //允许 JSON 结尾有逗号
            };
            // 使用 JsonSerializer.Deserialize 方法进行反序列化
            var ins = JsonSerializer.Deserialize<T>(json, options);
            if (ins == null)
            {
                return default;
            }
            return ins;
        }

        // 将对象序列化为 JSON 字符串的扩展方法（未设置规范的）
        public static string JsonSe<T>(this T obj, JsonSerializerOptions options) where T : class
        {
            // 使用 JsonSerializer.Serialize 方法进行序列化
            return JsonSerializer.Serialize(obj, options);
        }

        // 将 JSON 字符串反序列化为对象的扩展方法（未设置规范的）
        public static T? JsonDe<T>(this string json, JsonSerializerOptions options) where T : class
        {
            // 使用 JsonSerializer.Deserialize 方法进行反序列化
            return JsonSerializer.Deserialize<T>(json, options);
        }

        public class LowerCaseNamingPolicy : JsonNamingPolicy
        {
            // 重写 ConvertName 方法，将名称转换为全小写
            public override string ConvertName(string name) => name.ToLowerInvariant();
        }
    }

    public class FlexibleBoolConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 如果 JSON 值是布尔类型 (true/false)
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;

            // 如果 JSON 值是字符串 ("true"/"false")
            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString();
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
                return reader.GetInt32() != 0;
            }

            return false; // 默认值
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}