using System.Text;

namespace CommonHelp;

/// <summary>网页抓取结果：最终地址、标题、正文纯文本。</summary>
public sealed record WebPageContent(string Url, string Title, string Text, bool Truncated);

/// <summary>
/// 功能职责：抓取网页并把 HTML 精简成适合喂给模型的纯文本。
/// 不做搜索，不管理容器与设置，不判断权限。
/// 方法群组：网页抓取；正文转换与标题抽取委托 HtmlTextHelp。
/// </summary>
public sealed class WebPageClient
{
    /// <summary>正文返回的默认字符上限。</summary>
    public const int DefaultMaxCharacters = 8_000;

    /// <summary>正文返回的字符上限最大值。</summary>
    public const int MaxCharactersLimit = 40_000;

    /// <summary>未指定产品名时使用的 User-Agent。</summary>
    public const string DefaultUserAgent = "Mozilla/5.0 (compatible; WebPageClient/1.0)";

    // 只读响应体前 600 KB：超出的部分对正文没有意义，避免整站下载吃内存。
    private const int MaxRawBytes = 600_000;
    private const int ReadBufferSize = 16 * 1024;

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);
    private static readonly HttpClient SharedClient = CreateClient();

    static WebPageClient()
    {
        // 中文站点常用 gb2312 / gbk 声明；未注册时 GetEncoding 直接抛异常。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly HttpClient _http;
    private readonly string? _userAgent;

    /// <summary>
    /// 功能作用：记下要使用的 HttpClient 与可选 User-Agent。不发起请求。
    /// </summary>
    /// <param name="http">自定义客户端；为空则用库内共享客户端。</param>
    /// <param name="userAgent">请求头 User-Agent；为空则沿用客户端默认值。</param>
    public WebPageClient(HttpClient? http = null, string? userAgent = null)
    {
        _http = http ?? SharedClient;
        _userAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
    }

    /// <summary>
    /// 功能作用：抓取网页，返回标题与正文纯文本；HTML 走 HtmlTextHelp 去标签压缩空白，其他类型（JSON / 纯文本）原样返回。
    /// 输入：<paramref name="url"/> http/https 绝对地址；<paramref name="maxCharacters"/> 正文字符上限，收在 1–40000。
    /// 输出：抓取结果；正文超上限时截断并置 <see cref="WebPageContent.Truncated"/>。
    /// 异步：可等待；30 秒超时以 TaskCanceledException 结束、HTTP 失败抛 HttpRequestException，均由调用方转错误提示。
    /// </summary>
    public async Task<WebPageContent> FetchAsync(
        string url,
        int maxCharacters = DefaultMaxCharacters,
        CancellationToken cancellationToken = default)
    {
        var target = NormalizeUrl(url);
        var limit = Math.Clamp(maxCharacters, 1, MaxCharactersLimit);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(FetchTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        if (_userAgent is not null)
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType;
        var isHtml = contentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;
        await using var stream = await response.Content
            .ReadAsStreamAsync(timeoutCts.Token)
            .ConfigureAwait(false);
        var raw = Decode(await ReadBoundedAsync(stream, timeoutCts.Token).ConfigureAwait(false), contentType?.CharSet);
        var text = isHtml ? HtmlTextHelp.ToPlainText(raw) : raw.Trim();
        var truncated = text.Length > limit;

        return new WebPageContent(
            target,
            isHtml ? HtmlTextHelp.ExtractTitle(raw) : string.Empty,
            truncated ? text[..limit] : text,
            truncated);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        using var memory = new MemoryStream();
        while (memory.Length < MaxRawBytes)
        {
            var remaining = (int)Math.Min(buffer.Length, MaxRawBytes - memory.Length);
            var read = await stream
                .ReadAsync(buffer.AsMemory(0, remaining), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    /// <summary>功能作用：按响应头 charset 解码字节；未声明或声明了未知编码时回退 UTF-8。</summary>
    private static string Decode(byte[] bytes, string? charSet)
    {
        if (string.IsNullOrWhiteSpace(charSet))
            return Encoding.UTF8.GetString(bytes);

        try
        {
            return Encoding.GetEncoding(charSet.Trim().Trim('"')).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException($"仅支持 http/https 地址：{url}", nameof(url));

        return uri.AbsoluteUri;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = FetchTimeout + TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        return client;
    }
}
