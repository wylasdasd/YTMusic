namespace CommonHelp;

/// <summary>
/// 功能职责：工具操作的统一返回封装。成功时 <see cref="Value"/> 携带结果，失败时 <see cref="Error"/> 携带用户可读说明。不携带 HTTP/业务状态码。
/// 方法群组：构建成功结果、构建失败结果。
/// </summary>
public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    /// <summary>
    /// 功能作用：构建成功结果。
    /// </summary>
    /// <param name="value">成功时的返回值。</param>
    /// <returns>IsSuccess 为 true 的结果。</returns>
    public static Result<T> Ok(T value) => new(true, value, null);

    /// <summary>
    /// 功能作用：构建失败结果并携带错误说明。
    /// </summary>
    /// <param name="error">用户可读的错误说明。</param>
    /// <returns>IsSuccess 为 false 的结果。</returns>
    public static Result<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// 功能职责：带 HTTP 状态码语义的返回封装。以 200–299 为成功。不负责发请求。
/// 方法群组：构建成功结果、构建失败结果。
/// </summary>
public sealed record HttpResult<T>(int Code, T? Value, string? Error)
{
    public bool IsSuccess => Code >= 200 && Code < 300;

    /// <summary>
    /// 功能作用：构建成功结果，默认 HTTP 200。
    /// </summary>
    /// <param name="value">成功时的返回值。</param>
    /// <param name="code">HTTP 状态码，默认 200。</param>
    /// <returns>Code 在 200–299 范围内的结果。</returns>
    public static HttpResult<T> Ok(T value, int code = 200)
        => new(code, value, null);

    /// <summary>
    /// 功能作用：构建失败结果，默认 HTTP 500。
    /// </summary>
    /// <param name="error">用户可读的错误说明。</param>
    /// <param name="code">HTTP 状态码，默认 500。</param>
    /// <returns>失败结果。</returns>
    public static HttpResult<T> Fail(string error, int code = 500)
        => new(code, default, error);
}

/// <summary>
/// 功能职责：兼容旧业务的结果类型；约定 Code == 0 为成功。新代码请用 <see cref="Result{T}"/> 或 <see cref="HttpResult{T}"/>。
/// 方法群组：构建成功结果、构建失败结果。
/// </summary>
public sealed record BLLResult<T>(int Code, T? Value, string? Error)
{
    public bool IsSuccess => Code == 0;

    /// <summary>
    /// 功能作用：构建成功结果，默认业务码 0。
    /// </summary>
    /// <param name="value">成功时的返回值。</param>
    /// <param name="code">业务码，默认 0。</param>
    /// <returns>Code 为 0 的成功结果。</returns>
    public static BLLResult<T> Ok(T value, int code = 0)
        => new(code, value, null);

    /// <summary>
    /// 功能作用：构建失败结果，默认业务码 -1。
    /// </summary>
    /// <param name="error">用户可读的错误说明。</param>
    /// <param name="code">业务码，默认 -1。</param>
    /// <returns>失败结果。</returns>
    public static BLLResult<T> Fail(string error, int code = -1)
        => new(code, default, error);
}
