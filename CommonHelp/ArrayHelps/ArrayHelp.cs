using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTool.ArrayHelps
{
    public static class ArrayHelp
    {

        public static void Shuffle<T>(this T[] array)
        {
            int n = array.Length;
            // 从最后一个元素开始，向前遍历
            for (int i = n - 1; i > 0; i--)
            {
                // 在 0 到 i 之间随机选择一个索引 j
                int j = Random.Shared.Next(0, i + 1);

                // 交换 array[i] 和 array[j]
                T temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        public static void Shuffle<T>(this IList<T> array)
        {
            int n = array.Count();
            for (int i = n - 1; i > 0; i--)
            {
                // 在 0 到 i 之间随机选择一个索引 j
                int j = Random.Shared.Next(0, i + 1);
                // 交换 array[i] 和 array[j]
                T temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }
    }

}
