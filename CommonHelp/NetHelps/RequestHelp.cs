using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CommonHelp;

/// <summary>
/// 功能职责：HTTP 发送公共核心——组请求、超时发送、读响应。不负责提供 HttpClient（子类实现 GetClient），无重试（见 <see cref="HttpTransportHelp"/>）。
/// 方法群组：发送并反序列化、只取状态码、原始发送、组装请求。
/// </summary>
public abstract class HttpExec
{
    /// <summary>
    /// 功能作用：提供本次请求使用的 HttpClient（注入实例或 Factory 创建）。
    /// </summary>
    /// <returns>HttpClient 实例。</returns>
    protected abstract HttpClient GetClient();

    /// <summary>失败时写日志；无日志的实现返回 null，错误仍按约定返回 default 或抛出。</summary>
    protected virtual ILogger? ErrorLogger => null;

    /// <summary>
    /// 功能作用：发送请求并反序列化响应。非 2xx、超时/取消、网络错误、JSON 解析失败返回 default。T 为 string 时直接返回正文。
    /// </summary>
    /// <param name="request">已组装的请求；本方法会释放。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="operation">日志用操作名。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    [RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    protected async Task<T?> SendAndDecodeAsync<T>(HttpRequestMessage request, int timeoutSeconds, string operation)
        where T : class
    {
        using (request)
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            try
            {
                using var response = await GetClient().SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                    return default;

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                return typeof(T) == typeof(string)
                    ? (T?)(object)content
                    : content.JsonDe<T>(JsonExtensions.LenientDeserializeOptions);
            }
            catch (OperationCanceledException)
            {
                ErrorLogger?.LogError("{Operation} timed out or was canceled.", operation);
                return default;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                ErrorLogger?.LogError(ex, "{Operation} failed.", operation);
                return default;
            }
        }
    }

    /// <summary>
    /// 功能作用：发送请求并反序列化响应（源生成元数据，AOT / Trim 安全）。非 2xx、超时/取消、网络错误、JSON 解析失败返回 default。
    /// </summary>
    /// <param name="request">已组装的请求；本方法会释放。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="operation">日志用操作名。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    protected async Task<T?> SendAndDecodeAsync<T>(HttpRequestMessage request, JsonTypeInfo<T> typeInfo, int timeoutSeconds, string operation)
        where T : class
    {
        using (request)
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            try
            {
                using var response = await GetClient().SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                    return default;

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                return typeof(T) == typeof(string)
                    ? (T?)(object)content
                    : content.JsonDe(typeInfo);
            }
            catch (OperationCanceledException)
            {
                ErrorLogger?.LogError("{Operation} timed out or was canceled.", operation);
                return default;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                ErrorLogger?.LogError(ex, "{Operation} failed.", operation);
                return default;
            }
        }
    }

    /// <summary>
    /// 功能作用：发送请求，只返回 HTTP 状态码；无超时。本方法会释放 request。
    /// </summary>
    /// <param name="request">已组装的请求。</param>
    /// <returns>HTTP 状态码。</returns>
    /// <remarks>异步：可等待。</remarks>
    protected async Task<int> SendStatusAsync(HttpRequestMessage request)
    {
        using (request)
        using (var response = await GetClient().SendAsync(request))
            return (int)response.StatusCode;
    }

    /// <summary>
    /// 功能作用：发送原始请求并返回响应（不反序列化、不吞异常）。响应由调用方读取并 Dispose；请求在发送完成后释放。
    /// </summary>
    /// <param name="request">已组装的请求。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>未经处理的 HTTP 响应。</returns>
    /// <remarks>异步：可等待。</remarks>
    protected async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using (request)
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return await GetClient().SendAsync(request, cts.Token);
        }
    }

    /// <summary>
    /// 功能作用：组装字符串 JSON 请求；body 为空则无请求体（GET/DELETE）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="body">JSON 字符串；null 则无请求体。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="mediaType">媒体类型，默认 application/json。</param>
    /// <returns>已组装的请求。</returns>
    protected static HttpRequestMessage CreateStringRequest(
        HttpMethod method, string url, string? body, Action<HttpRequestHeaders>? action, string? mediaType = null)
    {
        var request = new HttpRequestMessage(method, url);
        action?.Invoke(request.Headers);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, mediaType ?? "application/json");
        return request;
    }

    /// <summary>
    /// 功能作用：组装强类型 JSON 请求：对象序列化为缩进、中文不转义的 JSON。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="body">请求对象；null 则无请求体。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="mediaType">媒体类型，默认 application/json。</param>
    /// <returns>已组装的请求。</returns>
    [RequiresDynamicCode("JSON 序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
    [RequiresUnreferencedCode("JSON 序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
    protected static HttpRequestMessage CreateObjectRequest<T>(
        HttpMethod method, string url, T? body, Action<HttpRequestHeaders>? action, string? mediaType = null)
    {
        var request = new HttpRequestMessage(method, url);
        action?.Invoke(request.Headers);
        if (body is not null)
            request.Content = new StringContent(body.JsonSe(JsonExtensions.IndentedRelaxedOptions), Encoding.UTF8, mediaType ?? "application/json");
        return request;
    }

    /// <summary>
    /// 功能作用：组装强类型 JSON 请求（源生成元数据，AOT / Trim 安全）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="body">请求对象；null 则无请求体。</param>
    /// <param name="typeInfo">源生成类型元数据。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="mediaType">媒体类型，默认 application/json。</param>
    /// <returns>已组装的请求。</returns>
    protected static HttpRequestMessage CreateObjectRequest<T>(
        HttpMethod method, string url, T? body, JsonTypeInfo<T> typeInfo, Action<HttpRequestHeaders>? action, string? mediaType = null)
    {
        var request = new HttpRequestMessage(method, url);
        action?.Invoke(request.Headers);
        if (body is not null)
            request.Content = new StringContent(body.JsonSe(typeInfo), Encoding.UTF8, mediaType ?? "application/json");
        return request;
    }

    /// <summary>
    /// 功能作用：组装 application/x-www-form-urlencoded POST。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="formData">表单字段。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>已组装的请求。</returns>
    protected static HttpRequestMessage CreateFormRequest(
        string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        action?.Invoke(request.Headers);
        request.Content = new FormUrlEncodedContent(
            formData.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? ""));
        return request;
    }
}

/// <summary>
/// 功能职责：字符串请求体的 <see cref="IHttpService"/> 实现。调用方自己把 JSON 字符串准备好再传入。不负责拿 HttpClient。
/// 方法群组：JSON/表单请求、原始发送、只取状态码。
/// </summary>
[RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public abstract class StringBodyHttpService : HttpExec, IHttpService
{
    /// <inheritdoc />
    public virtual Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where T : class =>
        SendAndDecodeAsync<T>(CreateStringRequest(HttpMethod.Post, url, body, action), timeoutSeconds, nameof(PostRequestAsync));

    /// <inheritdoc />
    public virtual Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where T : class =>
        SendAndDecodeAsync<T>(CreateStringRequest(HttpMethod.Put, url, body, action), timeoutSeconds, nameof(PutRequestAsync));

    /// <inheritdoc />
    public virtual Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where T : class =>
        SendAndDecodeAsync<T>(CreateStringRequest(HttpMethod.Delete, url, null, action), timeoutSeconds, nameof(DeleteRequestAsync));

    /// <inheritdoc />
    public virtual Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where T : class =>
        SendAndDecodeAsync<T>(CreateStringRequest(HttpMethod.Get, url, null, action), timeoutSeconds, nameof(GetRequestAsync));

    /// <inheritdoc />
    public virtual Task<T?> PostFormRequestAsync<T>(
        string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 3)
        where T : class =>
        SendAndDecodeAsync<T>(CreateFormRequest(url, formData, action), timeoutSeconds, nameof(PostFormRequestAsync));

    /// <inheritdoc />
    public virtual Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, string? body = null, string? mediaType = null, int timeoutSeconds = 30,
        Action<HttpRequestHeaders>? action = null, CancellationToken cancellationToken = default) =>
        SendRawAsync(CreateStringRequest(method, url, body, action, mediaType), timeoutSeconds, cancellationToken);

    /// <inheritdoc />
    public virtual Task<int> SendRequestAsync(HttpMethod method, string url, string body, Action<HttpRequestHeaders>? action = null) =>
        SendStatusAsync(CreateStringRequest(method, url, body, action));
}

/// <summary>
/// 功能职责：强类型请求体的 <see cref="ITypedHttpService"/> 实现。内部把对象序列化成 JSON 再发送。不负责拿 HttpClient。
/// 方法群组：JSON/表单请求、原始发送、只取状态码。
/// </summary>
[RequiresDynamicCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public abstract class TypedBodyHttpService : HttpExec, ITypedHttpService
{
    /// <inheritdoc />
    public virtual async Task<int> SendRequestAsync<TRequest>(
        HttpMethod method, string url, TRequest requestBody, Action<HttpRequestHeaders>? action = null)
    {
        try
        {
            return await SendStatusAsync(CreateObjectRequest(method, url, requestBody, action));
        }
        catch (Exception ex)
        {
            ErrorLogger?.LogError(ex, "{Method} {Url} failed.", method, url);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual Task<TResponse?> PostRequestAsync<TRequest, TResponse>(
        string url, TRequest requestBody, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class =>
        SendAndDecodeAsync<TResponse>(CreateObjectRequest(HttpMethod.Post, url, requestBody, action), timeoutSeconds, $"{HttpMethod.Post} {url}");

    /// <inheritdoc />
    public virtual Task<TResponse?> PutRequestAsync<TRequest, TResponse>(
        string url, TRequest requestBody, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class =>
        SendAndDecodeAsync<TResponse>(CreateObjectRequest(HttpMethod.Put, url, requestBody, action), timeoutSeconds, $"{HttpMethod.Put} {url}");

    /// <inheritdoc />
    public virtual Task<TResponse?> DeleteRequestAsync<TResponse>(
        string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class =>
        SendAndDecodeAsync<TResponse>(CreateObjectRequest<object>(HttpMethod.Delete, url, null, action), timeoutSeconds, $"{HttpMethod.Delete} {url}");

    /// <inheritdoc />
    public virtual Task<TResponse?> GetRequestAsync<TResponse>(
        string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class =>
        SendAndDecodeAsync<TResponse>(CreateObjectRequest<object>(HttpMethod.Get, url, null, action), timeoutSeconds, $"{HttpMethod.Get} {url}");

    /// <inheritdoc />
    public virtual Task<TResponse?> PostFormRequestAsync<TResponse>(
        string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 3)
        where TResponse : class =>
        SendAndDecodeAsync<TResponse>(CreateFormRequest(url, formData, action), timeoutSeconds, $"POST form {url}");

    /// <inheritdoc />
    public virtual Task<HttpResponseMessage> SendAsync<TRequest>(
        HttpMethod method, string url, TRequest? requestBody = default, string? mediaType = null, int timeoutSeconds = 30,
        Action<HttpRequestHeaders>? action = null, CancellationToken cancellationToken = default) =>
        SendRawAsync(CreateObjectRequest(method, url, requestBody, action, mediaType), timeoutSeconds, cancellationToken);
}

/// <summary>
/// 功能职责：用调用方提供的 HttpClient 发字符串 JSON 请求。不依赖 IHttpClientFactory 和日志。
/// 方法群组：见 <see cref="IHttpService"/>。
/// </summary>
[RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public class RequestHelp : StringBodyHttpService
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// 功能作用：注入 HttpClient 创建请求服务。
    /// </summary>
    /// <param name="httpClient">调用方提供的客户端。</param>
    public RequestHelp(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    protected override HttpClient GetClient() => httpClient;
}

/// <summary>
/// 功能职责：基于 IHttpClientFactory 的字符串 JSON 请求服务。超时或异常时返回 default 并记日志。
/// 方法群组：见 <see cref="IHttpService"/>。
/// </summary>
[RequiresDynamicCode("JSON 反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public class HttpClientFactoryService : StringBodyHttpService
{
    protected readonly IHttpClientFactory httpClientFactory;
    protected readonly ILogger<IHttpService> logger;

    /// <summary>
    /// 功能作用：注入 IHttpClientFactory 和日志服务。
    /// </summary>
    /// <param name="httpClientFactory">客户端工厂。</param>
    /// <param name="logger">失败日志。</param>
    public HttpClientFactoryService(IHttpClientFactory httpClientFactory, ILogger<IHttpService> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override HttpClient GetClient() => httpClientFactory.CreateClient();

    /// <inheritdoc />
    protected override ILogger? ErrorLogger => logger;
}

/// <summary>
/// 功能职责：用调用方提供的 HttpClient 发强类型 JSON 请求。不依赖 IHttpClientFactory 和日志。
/// 方法群组：见 <see cref="ITypedHttpService"/>。
/// </summary>
[RequiresDynamicCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public class TypedRequestHelp : TypedBodyHttpService
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// 功能作用：注入 HttpClient 创建强类型请求服务。
    /// </summary>
    /// <param name="httpClient">调用方提供的客户端。</param>
    public TypedRequestHelp(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    protected override HttpClient GetClient() => httpClient;
}

/// <summary>
/// 功能职责：基于 IHttpClientFactory 的强类型 JSON 请求服务。请求对象内部序列化，响应反序列化为 TResponse。
/// 方法群组：见 <see cref="ITypedHttpService"/>。
/// </summary>
[RequiresDynamicCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Native AOT 请使用 JsonTypeInfo 重载。")]
[RequiresUnreferencedCode("JSON 序列化/反序列化可能依赖无法静态分析的类型；Trim/AOT 请使用 JsonTypeInfo 重载。")]
public class TypedHttpClientFactoryService : TypedBodyHttpService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<ITypedHttpService> logger;

    /// <summary>
    /// 功能作用：注入 IHttpClientFactory 和日志服务。
    /// </summary>
    /// <param name="httpClientFactory">客户端工厂。</param>
    /// <param name="logger">失败日志。</param>
    public TypedHttpClientFactoryService(IHttpClientFactory httpClientFactory, ILogger<ITypedHttpService> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override HttpClient GetClient() => httpClientFactory.CreateClient();

    /// <inheritdoc />
    protected override ILogger? ErrorLogger => logger;
}

/// <summary>
/// 功能职责：强类型 JSON 请求服务接口。TRequest 为请求对象，TResponse 为响应类型。不规定 HttpClient 来源。
/// 方法群组：JSON/表单请求、原始发送、只取状态码。
/// </summary>
public interface ITypedHttpService
{
    /// <summary>
    /// 功能作用：用强类型请求对象发送 JSON，只返回 HTTP 状态码。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="requestBody">请求对象。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>HTTP 状态码。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<int> SendRequestAsync<TRequest>(HttpMethod method, string url, TRequest requestBody, Action<HttpRequestHeaders>? action = null);

    /// <summary>
    /// 功能作用：用强类型请求对象发送 JSON POST，并把响应反序列化为 TResponse。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="requestBody">请求对象。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<TResponse?> PostRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class;

    /// <summary>
    /// 功能作用：用强类型请求对象发送 JSON PUT，并把响应反序列化为 TResponse。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="requestBody">请求对象。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<TResponse?> PutRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class;

    /// <summary>
    /// 功能作用：发送 DELETE 并把响应反序列化为 TResponse。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<TResponse?> DeleteRequestAsync<TResponse>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class;

    /// <summary>
    /// 功能作用：发送 GET 并把响应反序列化为 TResponse。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<TResponse?> GetRequestAsync<TResponse>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null)
        where TResponse : class;

    /// <summary>
    /// 功能作用：发送 urlencoded 表单并把响应反序列化为 TResponse。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="formData">表单字段。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="timeoutSeconds">超时秒数，默认较长。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<TResponse?> PostFormRequestAsync<TResponse>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 3)
        where TResponse : class;

    /// <summary>
    /// 功能作用：用强类型请求对象发送原始 HTTP 请求。不吞异常、不反序列化；响应由调用方释放。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="requestBody">请求对象；null 则无请求体。</param>
    /// <param name="mediaType">媒体类型。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>未经处理的 HTTP 响应。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<HttpResponseMessage> SendAsync<TRequest>(HttpMethod method, string url, TRequest? requestBody = default, string? mediaType = null, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 功能职责：字符串 JSON/表单请求服务接口。失败或超时返回 default；需要原始响应时用 <see cref="SendAsync"/>。
/// 方法群组：JSON/表单请求、原始发送、只取状态码。
/// </summary>
public interface IHttpService
{
    /// <summary>
    /// 功能作用：POST JSON 并把响应反序列化为 T；T 为 string 时直接返回响应文本。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="body">已准备好的 JSON 字符串。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

    /// <summary>
    /// 功能作用：PUT JSON 并把响应反序列化为 T。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="body">已准备好的 JSON 字符串。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

    /// <summary>
    /// 功能作用：DELETE 并把响应反序列化为 T；请求无 body。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

    /// <summary>
    /// 功能作用：GET 并把响应反序列化为 T；请求无 body。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

    /// <summary>
    /// 功能作用：POST urlencoded 表单并把响应反序列化为 T。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="formData">表单字段。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="timeoutSeconds">超时秒数，默认较长。</param>
    /// <returns>反序列化结果；失败为 default。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<T?> PostFormRequestAsync<T>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 3) where T : class;

    /// <summary>
    /// 功能作用：发送原始 HTTP 请求并返回未经处理的响应。不吞异常、不反序列化；响应由调用方释放。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="body">请求体；null 则无请求体。</param>
    /// <param name="mediaType">媒体类型，缺省 application/json。</param>
    /// <param name="timeoutSeconds">超时秒数。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>未经处理的 HTTP 响应。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? body = null, string? mediaType = null, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 功能作用：用 JSON 请求体发送指定 HTTP 方法，只返回状态码。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="url">请求地址。</param>
    /// <param name="body">JSON 字符串。</param>
    /// <param name="action">可选请求头配置。</param>
    /// <returns>HTTP 状态码。</returns>
    /// <remarks>异步：可等待。</remarks>
    Task<int> SendRequestAsync(HttpMethod method, string url, string body, Action<HttpRequestHeaders>? action = null);
}
