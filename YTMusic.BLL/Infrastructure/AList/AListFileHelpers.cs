using CommonHelp;

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
        if (!MimeHelp.TryParseDataUrl(dataUri, out _, out var base64Part, out _)
            || !MimeHelp.TryDecodeBase64(base64Part, out var bytes))
        {
            throw new InvalidOperationException("Invalid data URI cover content.");
        }

        return new MemoryStream(bytes, writable: false);
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

    public static void TryDeleteFile(string path) => FileHelp.DeleteIfExists(path);
}
