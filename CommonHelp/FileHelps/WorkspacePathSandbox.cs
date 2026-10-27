namespace CommonHelp;

/// <summary>
/// 功能职责：根目录与额外允许目录的路径解析、边界判定，以及软链接 / 目录联接真实目标防逃逸。不读取产品设置。
/// 方法群组：根目录包含判定、可读路径解析、可写路径解析、根内相对路径解析。
/// </summary>
public static class WorkspacePathSandbox
{
    /// <summary>
    /// 功能作用：判断 <paramref name="path"/> 是否位于 <paramref name="root"/> 目录内（含根目录本身），并且不存在指向 <paramref name="root"/> 之外的软链接或目录联接。
    /// 输入：待检路径与允许的根目录。
    /// 输出：在边界内且无软链接逃逸返回 true。
    /// </summary>
    public static bool IsWithinRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!IsLexicallyWithinRoot(fullPath, fullRoot))
            return false;

        return !HasLinkEscape(fullPath, fullRoot);
    }

    /// <summary>
    /// 功能作用：在主根目录与额外允许目录中解析可读路径。相对路径先试主根；绝对路径须落在主根内。主根未命中且 <paramref name="allowOutsideRoot"/> 为 true 时，再在额外目录中解析。
    /// 输入：主根、请求路径、是否允许主根之外、额外允许目录。
    /// 输出：命中的规范化绝对路径；越界或软链接逃逸时返回 null。
    /// </summary>
    public static string? ResolveAllowedReadPath(
        string root,
        string? requestedPath,
        bool allowOutsideRoot,
        IEnumerable<string>? allowedDirectories)
    {
        var raw = requestedPath ?? string.Empty;
        if (!Path.IsPathRooted(raw))
        {
            var inRoot = Path.GetFullPath(Path.Combine(root, raw));
            if (IsWithinRoot(inRoot, root))
                return inRoot;
        }
        else
        {
            var rooted = Path.GetFullPath(raw);
            if (IsWithinRoot(rooted, root))
                return rooted;
        }

        if (!allowOutsideRoot || allowedDirectories is null)
            return null;

        return ResolveInAllowedDirectories(raw, allowedDirectories);
    }

    /// <summary>
    /// 功能作用：在主根目录与额外允许目录中解析可写路径。相对路径与落在主根内的绝对路径优先；否则在额外目录中解析。
    /// 输入：主根、请求路径、额外允许目录。
    /// 输出：命中的规范化绝对路径；越界或软链接逃逸时返回 null。
    /// </summary>
    public static string? ResolveAllowedEditPath(
        string root,
        string requestedPath,
        IEnumerable<string>? allowedDirectories)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            return null;

        if (!Path.IsPathRooted(requestedPath))
        {
            var inRoot = Path.GetFullPath(Path.Combine(root, requestedPath));
            if (IsWithinRoot(inRoot, root))
                return inRoot;
        }
        else
        {
            var rooted = Path.GetFullPath(requestedPath);
            if (IsWithinRoot(rooted, root))
                return rooted;
        }

        return allowedDirectories is null
            ? null
            : ResolveInAllowedDirectories(requestedPath, allowedDirectories);
    }

    /// <summary>
    /// 功能作用：只在根目录内解析相对路径。
    /// 输入：根目录与相对路径。
    /// 输出：根内的规范化绝对路径；绝对路径、越界或软链接逃逸返回 null。
    /// </summary>
    public static string? ResolveRelativePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return null;

        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        return IsWithinRoot(candidate, root) ? candidate : null;
    }

    private static string? ResolveInAllowedDirectories(string rawPath, IEnumerable<string> allowedDirectories)
    {
        foreach (var dir in allowedDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var root = Path.GetFullPath(dir);
            var candidate = Path.IsPathRooted(rawPath)
                ? Path.GetFullPath(rawPath)
                : Path.GetFullPath(Path.Combine(root, rawPath));
            if (IsWithinRoot(candidate, root))
                return candidate;
        }

        return null;
    }

    private static bool IsLexicallyWithinRoot(string fullPath, string fullRoot)
    {
        if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var rootWithSep = fullRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 功能作用：沿路径向上检查已存在的文件或目录是否包含指向 <paramref name="fullRoot"/> 之外的软链接/目录联接。
    /// </summary>
    private static bool HasLinkEscape(string fullPath, string fullRoot)
    {
        try
        {
            var rootFinal = ResolveFinalTargetPath(fullRoot) ?? fullRoot;
            var current = fullPath;
            while (!string.IsNullOrEmpty(current) && IsLexicallyWithinRoot(current, fullRoot))
            {
                if (File.Exists(current))
                {
                    var target = new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true);
                    if (target is not null && !IsLexicallyWithinRoot(Path.GetFullPath(target.FullName), rootFinal))
                        return true;
                }
                else if (Directory.Exists(current))
                {
                    var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
                    if (target is not null && !IsLexicallyWithinRoot(Path.GetFullPath(target.FullName), rootFinal))
                        return true;
                }

                if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
                    break;

                current = Path.GetDirectoryName(current) ?? string.Empty;
            }
        }
        catch
        {
            // 文件系统不支持链接查询或权限受限时保守按无链接处理，交由后续 I/O 权限检查。
        }

        return false;
    }

    private static string? ResolveFinalTargetPath(string existingDirectory)
    {
        if (!Directory.Exists(existingDirectory))
            return null;

        var target = new DirectoryInfo(existingDirectory).ResolveLinkTarget(returnFinalTarget: true);
        return target is null
            ? null
            : Path.GetFullPath(target.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
