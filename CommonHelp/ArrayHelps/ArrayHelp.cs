namespace CommonHelp;

/// <summary>
/// 功能职责：数组与列表原地随机重排。不负责生成随机数源、不拷贝新集合。
/// 方法群组：洗牌。
/// </summary>
public static class ArrayHelp
{
    /// <summary>
    /// 功能作用：Fisher–Yates 洗牌，原地随机打乱数组元素顺序。
    /// </summary>
    /// <param name="array">要打乱的数组。</param>
    /// <returns>无返回值；直接改动传入数组的元素顺序。</returns>
    public static void Shuffle<T>(this T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// 功能作用：Fisher–Yates 洗牌，原地随机打乱列表元素顺序。
    /// </summary>
    /// <param name="array">要打乱的列表。</param>
    /// <returns>无返回值；直接改动传入列表的元素顺序。</returns>
    public static void Shuffle<T>(this IList<T> array)
    {
        for (int i = array.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
