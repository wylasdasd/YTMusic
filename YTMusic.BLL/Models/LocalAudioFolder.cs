using System.Security.Cryptography;
using System.Text;

namespace YTMusic.BLL.Models;

/// <summary>把一个本地目录扫成可导入的音频列表，并生成不与 YouTube 冲突的曲目 id。</summary>
public static class LocalAudioFolder
{
    public static readonly string[] AudioExtensions = [".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg"];
    public const string VideoIdPrefix = "local:";

    public static bool IsImportedVideoId(string? videoId)
        => !string.IsNullOrEmpty(videoId) && videoId.StartsWith(VideoIdPrefix, StringComparison.Ordinal);

    public static IReadOnlyList<string> FindAudioFiles(string directory)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pending.Push(entry);
                    continue;
                }

                var extension = Path.GetExtension(entry);
                if (AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(Path.GetFullPath(entry));
                }
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public static string CreateVideoId(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return VideoIdPrefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string AllocateFolderName(string? desired, IEnumerable<string> existingNames)
    {
        var trimmed = string.IsNullOrWhiteSpace(desired) ? "本地音乐" : desired.Trim();
        var used = new HashSet<string>(
            existingNames.Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);
        if (used.Add(trimmed))
        {
            return trimmed;
        }

        for (var index = 2; index < 10000; index++)
        {
            var candidate = $"{trimmed} ({index})";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }

        return $"{trimmed} ({DateTime.UtcNow:yyyyMMddHHmmss})";
    }

    public static string TrackTitle(string filePath)
    {
        var title = Path.GetFileNameWithoutExtension(filePath);
        return string.IsNullOrWhiteSpace(title) ? Path.GetFileName(filePath) : title;
    }
}
