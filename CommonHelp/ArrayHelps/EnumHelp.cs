using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CommonTool.ArrayHelps
{
    /// <summary>
    /// 枚举类型帮助类
    /// </summary>
    public static class EnumHelp
    {
        /// <summary>
        /// 获取枚举值的描述信息 (DescriptionAttribute)
        /// </summary>
        /// <param name="value">枚举值</param>
        /// <returns>描述信息或枚举名称</returns>
        public static string GetDescription(this Enum value)
        {
            try
            {
                if (value == null) return string.Empty;

                var type = value.GetType();
                var name = Enum.GetName(type, value);
                if (name == null) return value.ToString();

                var field = type.GetField(name);
                if (field == null) return name;

                var attribute = field.GetCustomAttribute<DescriptionAttribute>();
                return attribute?.Description ?? name;
            }
            catch (Exception ex)
            {
                return $"枚举值:{value},没找到枚举类型名称";
            }
        }

        /// <summary>
        /// 将枚举转换为字典 (Key: 枚举值, Value: 描述或名称)
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns>字典</returns>
        public static Dictionary<int, string> ToDictionary<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .ToDictionary(e => Convert.ToInt32(e), e => GetDescription(e));
        }

        /// <summary>
        /// 获取枚举所有项的列表 (包含值、名称和描述)
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns>匿名对象列表</returns>
        public static List<EnumItemDetail> GetList<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new EnumItemDetail
                {
                    Value = Convert.ToInt32(e),
                    Name = e.ToString(),
                    Description = GetDescription(e)
                }).ToList();
        }

        /// <summary>
        /// 根据描述信息获取枚举值
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="description">描述信息</param>
        /// <returns>枚举值</returns>
        public static TEnum? GetEnumByDescription<TEnum>(string description) where TEnum : struct, Enum
        {
            foreach (var field in typeof(TEnum).GetFields())
            {
                var attribute = field.GetCustomAttribute<DescriptionAttribute>();
                if (attribute != null && attribute.Description == description)
                {
                    return (TEnum?)field.GetValue(null);
                }
                if (field.Name == description)
                {
                    return (TEnum?)field.GetValue(null);
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 枚举项详情
    /// </summary>
    public class EnumItemDetail
    {
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}