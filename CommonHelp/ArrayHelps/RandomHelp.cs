using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTool.ArrayHelps
{
    public class RandomHelp
    {
        /// <summary>
        ///从可遍历类型里面随机获取几个
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static List<T> GetRandomList<T>(IEnumerable<T> list, int count)
        {
            Random rng = new Random();
            List<T> result = new List<T>(list); // 拷贝一份防止修改原列表
            int n = result.Count;

            for (int i = 0; i < count && i < n; i++)
            {
                int r = i + rng.Next(n - i);
                T temp = result[r];
                result[r] = result[i];
                result[i] = temp;
            }

            return result.Take(count).ToList();
        }
    }
}