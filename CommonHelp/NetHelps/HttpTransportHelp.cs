using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CommonHelp;

/// <summary>
/// 功能职责：共享 HttpClient 的 JSON POST 构建、瞬时失败重试与响应体读取。适用于无 IHttpClientFactory 的高频 API（如 LLM）。不负责业务鉴权。
/// 方法群组：构建请求、带重试发送、读响应、原始发送。
/// </summary>
public class HttpTransportHelp
{
    private const int MaxSendAttempts = 3;
    private readonly HttpClient httpClient;

    /// <summary>
    /// 功能作用：注入 HttpClient 创建传输服务。
    /// </summary>
    /// <param name="httpClient">共享的 HttpClient。</param>
    public HttpTransportHelp(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 功能作用：把对象序列化为 UTF-8 JSON 并构建 application/json 内容（源生成元数据，AOT / Trim 安全）。
    /// </summary>
    /// <param name="payload">要序列化的对象。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <returns>带 Content-Type 的字节内容。</returns>
    public static ByteArrayContent CreateJsonContent<T>(T payload, JsonTypeInfo<T> typeInfo)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, typeInfo);
        return CreateJsonByteContent(bytes);
    }

    /// <summary>
    /// 功能作用：把对象序列化为 UTF-8 JSON 并构建 application/json 内容。
    /// </summary>
    /// <param name="payload">要序列化的对象。</param>
    /// <returns>带 Content-Type 的字节内容。</returns>
    [RequiresDynamicCode("JSON 序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    public static ByteArrayContent CreateJsonContent<T>(T payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return CreateJsonByteContent(bytes);
    }

    private static ByteArrayContent CreateJsonByteContent(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };
        return content;
    }

    /// <summary>
    /// 功能作用：构建 JSON POST 请求（HTTP/1.1 优先，允许降级）。
    /// </summary>
    /// <param name="uri">请求地址。</param>
    /// <param name="jsonBody">UTF-8 JSON 字节体。</param>
    /// <returns>已组装的 POST 请求。</returns>
    public static HttpRequestMessage CreateJsonPost(Uri uri, byte[] jsonBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = CreateJsonByteContent(jsonBody),
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        return request;
    }

    /// <summary>
    /// 功能作用：发送 JSON POST；瞬时网络失败最多重试 3 次（退避 400ms×次数）。
    /// </summary>
    /// <param name="uri">请求地址。</param>
    /// <param name="jsonBody">UTF-8 JSON 字节体。</param>
    /// <param name="configure">发送前自定义请求（如 Authorization）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>HTTP 响应。</returns>
    /// <remarks>异步：可等待。全部尝试失败则抛出。</remarks>
    public virtual async Task<HttpResponseMessage> SendJsonPostWithRetryAsync(
        Uri uri,
        byte[] jsonBody,
        Action<HttpRequestMessage>? configure,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            using var request = CreateJsonPost(uri, jsonBody);
            configure?.Invoke(request);

            try
            {
                return await SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxSendAttempts && !cancellationToken.IsCancellationRequested && IsTransientSendFailure(ex))
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw last ?? new HttpRequestException("JSON POST 请求发送失败。");
    }

    /// <summary>
    /// 功能作用：流式读取响应体为文本。
    /// </summary>
    /// <param name="response">HTTP 响应。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>响应正文。</returns>
    /// <remarks>异步：可等待。</remarks>
    public virtual async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 功能作用：发送原始 HTTP 请求；可在派生类中重写。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>HTTP 响应。</returns>
    /// <remarks>异步：可等待。</remarks>
    public virtual Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default) =>
        httpClient.SendAsync(request, cancellationToken);

    /// <summary>
    /// 功能作用：发送请求并按 completionOption 返回。流式读 body 时用 ResponseHeadersRead。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <param name="completionOption">完成选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>HTTP 响应。</returns>
    /// <remarks>异步：可等待。</remarks>
    public virtual Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken = default) =>
        httpClient.SendAsync(request, completionOption, cancellationToken);

    /// <summary>判断异常链中是否包含瞬时发送失败（可重试）。</summary>
    private static bool IsTransientSendFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or IOException or TaskCanceledException)
                return true;

            if (current.Message.Contains("copying content to a stream", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("An error occurred while sending the request", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
