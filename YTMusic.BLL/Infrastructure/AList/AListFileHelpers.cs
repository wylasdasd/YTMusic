namespace YTMusic.BLL.Infrastructure.AList;

/// <summary>AList 上传流程中的本地文件与临时文件工具。</summary>
internal static class AListFileHelpers
{
    public static bool TryGetLocalPath(string source, out string localPath)
    {
        localPath = string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (File.Exists(source))
        {
            localPath = source;
            return true;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            uri.IsFile &&
            File.Exists(uri.LocalPath))
        {
            localPath = uri.LocalPath;
            return true;
        }

        return false;
    }

    public static MemoryStream CreateDataUriStream(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex <= 0 || commaIndex == dataUri.Length - 1)
        {
            throw new InvalidOperationException("Invalid data URI cover content.");
        }

        var metadata = dataUri[..commaIndex];
        var payload = dataUri[(commaIndex + 1)..];
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only base64 data URI cover content is supported.");
        }

        try
        {
            return new MemoryStream(Convert.FromBase64String(payload), writable: false);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Invalid base64 cover content.", ex);
        }
    }

    public static async Task<string> WriteTempFileAsync(Stream sourceStream, string extension, CancellationToken cancellationToken)
    {
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension;
        var tempPath = Path.Combine(Path.GetTempPath(), $"ytmusic-upload-{Guid.NewGuid():N}{safeExtension}");
        await using (var fileStream = File.Create(tempPath))
        {
            await sourceStream.CopyToAsync(fileStream, cancellationToken);
        }

        return tempPath;
    }

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
