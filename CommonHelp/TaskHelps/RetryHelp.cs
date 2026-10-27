namespace CommonHelp;

/// <summary>
/// 功能职责：按调用方谓词做指数退避重试。不判断何种异常可重试。
/// 方法群组：执行并重试。
/// </summary>
public static class RetryHelp
{
    public const int DefaultMaxAttempts = 3;
    public const int DefaultBaseBackoffMs = 1_000;
    public const int DefaultMaxBackoffMs = 8_000;

    /// <summary>
    /// 功能作用：执行异步操作；失败且 <paramref name="shouldRetry"/> 为真时指数退避后再跑。
    /// </summary>
    /// <param name="action">要执行的操作。</param>
    /// <param name="shouldRetry">是否对这次异常再试；最后一轮失败不再问。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="maxAttempts">最多尝试次数（含首次）。</param>
    /// <param name="baseBackoffMs">首次重试等待毫秒；之后按 2 的幂放大。</param>
    /// <param name="maxBackoffMs">单次等待上限毫秒。</param>
    /// <returns>成功时的返回值。</returns>
    /// <remarks>异步：可等待。全部失败则抛出最后一次异常。</remarks>
    public static async Task<T> WithBackoffAsync<T>(
        Func<CancellationToken, Task<T>> action,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts,
        int baseBackoffMs = DefaultBaseBackoffMs,
        int maxBackoffMs = DefaultMaxBackoffMs)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(shouldRetry);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(baseBackoffMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBackoffMs);

        Exception? lastError = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxAttempts - 1 && shouldRetry(ex))
            {
                lastError = ex;
                var backoff = ComputeBackoffMs(baseBackoffMs, maxBackoffMs, attempt);
                if (backoff > 0)
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastError ?? new InvalidOperationException("重试失败。");
    }

    private static int ComputeBackoffMs(int baseBackoffMs, int maxBackoffMs, int attempt)
    {
        if (baseBackoffMs == 0 || maxBackoffMs == 0)
            return 0;

        var shift = Math.Min(attempt, 30);
        var doubled = (long)baseBackoffMs << shift;
        return (int)Math.Min(doubled, maxBackoffMs);
    }
}
