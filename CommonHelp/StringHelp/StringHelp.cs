using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CommonTool.StringHelp
{
    public class StringHelp
    {
        /// <summary>
        /// 计算给定文本的行数。支持自定义行分隔符（默认为 Environment.NewLine）。
        /// </summary>
        public static int CountLines(string text, string? lineSeparator = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            lineSeparator ??= Environment.NewLine;
            // 使用 Split 会处理末尾没有换行的情况
            return text.Split(new string[] { lineSeparator }, StringSplitOptions.None).Length;
        }

        /// <summary>
        /// 计算给定文本的字符数（基于字符串长度），默认按 UTF8 编码计算字节数也可通过参数更改。
        /// </summary>
        /// <param name="text">输入文本。</param>
        /// <param name="encoding">用于计算字节数的编码，若为 null 则返回字符数。</param>
        public static long CountCharacters(string text, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            if (encoding == null)
                return text.Length;
            return encoding.GetByteCount(text);
        }

        /// <summary>
        /// 读取文件并计算其行数与字符数（默认按 UTF8 编码计算字节数）。
        /// 返回元组： (lines, charsOrBytes) 。
        /// </summary>
        public static (long Lines, long Count) CountFileLinesAndChars(string filePath, Encoding? encoding = null)
        {
            if (!File.Exists(filePath))
                return (0, 0);

            encoding ??= Encoding.UTF8;
            long lines = 0;
            long count = 0;
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, encoding);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                lines++;
                count += encoding.GetByteCount(line) + encoding.GetByteCount(Environment.NewLine);
            }

            // 如果文件非空且没有换行结尾，上面的逻辑已正确统计每行；如果需要仅统计内容字节数可以另外返回
            return (lines, count);
        }

        /// <summary>
        /// 计算文件的字符数（或字节数，取决于 encoding 参数）。默认使用 UTF8 编码计算字节数。
        /// </summary>
        public static long CountFileCharacters(string filePath, Encoding? encoding = null)
        {
            if (!File.Exists(filePath))
                return 0;
            encoding ??= Encoding.UTF8;
            // 更高效的方式是按块读取并累加字节数
            long total = 0;
            var buffer = new char[8192];
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, encoding);
            int read;
            while ((read = sr.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (encoding == null)
                    total += read;
                else
                    total += encoding.GetByteCount(buffer, 0, read);
            }
            return total;
        }
    }
}
