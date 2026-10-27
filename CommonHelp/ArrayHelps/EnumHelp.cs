using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CommonHelp;
/// <summary>
/// 功能职责：通过 <see cref="DescriptionAttribute"/> 读枚举描述，并做列表/字典转换。不负责业务枚举定义。
/// 方法群组：读描述、转字典/列表、按描述反查。
/// </summary>
public static class EnumHelp
{
    private static readonly ConcurrentDictionary<Enum, string> DescriptionCache = new();

    /// <summary>
    /// 功能作用：取枚举值的 <c>[Description]</c> 文本；结果缓存。无描述时返回枚举名。
    /// </summary>
    /// <param name="value">枚举值。</param>
    /// <returns>描述文本或枚举名；value 为 null 时返回空串。</returns>
    public static string GetDescription(this Enum value)
    {
        if (value == null) return string.Empty;

        return DescriptionCache.GetOrAdd(value, static v => ResolveDescription(v));
    }

    private static string ResolveDescription(Enum value)
    {
        try
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null) return value.ToString();

            var field = type.GetField(name);
            if (field == null) return name;

            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? name;
        }
        catch (Exception)
        {
            return $"枚举值:{value},没找到枚举类型名称";
        }
    }

    /// <summary>
    /// 功能作用：把枚举全部值（int）与描述转为字典。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <returns>键为枚举整数值、值为描述或名称的字典。</returns>
    public static Dictionary<int, string> ToDictionary<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .ToDictionary(e => Convert.ToInt32(e), e => GetDescription(e));
    }

    /// <summary>
    /// 功能作用：返回枚举全部项的值、名称与描述。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <returns><see cref="EnumItemDetail"/> 列表。</returns>
    public static List<EnumItemDetail> GetList<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(e => new EnumItemDetail
            {
                Value = Convert.ToInt32(e),
                Name = e.ToString(),
                Description = GetDescription(e)
            }).ToList();
    }

    /// <summary>
    /// 功能作用：按描述文本（或字段名）反向查找枚举值。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <param name="description">描述或枚举字段名。</param>
    /// <returns>命中的枚举值；未找到则为 null。</returns>
    public static TEnum? GetEnumByDescription<TEnum>(string description) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(description))
            return null;

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            if ((attribute != null && attribute.Description == description) || field.Name == description)
            {
                return (TEnum?)field.GetValue(null);
            }
        }
        return null;
    }
}

/// <summary>
/// 功能职责：枚举项详情（整数值、枚举名、描述文本）。
/// </summary>
public class EnumItemDetail
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
