using Cronos;

namespace CommonHelp;

/// <summary>
/// 功能职责：计算固定间隔与 5 段 Cron 的下次触发时刻。不收间隔下限，Cron 无效时返回 null，不负责产品侧回退。
/// 方法群组：下次运行时间。
/// </summary>
public static class ScheduleTimingHelp
{
    /// <summary>
    /// 功能作用：按给定间隔计算下一次运行时刻。
    /// </summary>
    /// <param name="afterUtc">基准时刻。</param>
    /// <param name="intervalSeconds">间隔秒数，必须大于 0。</param>
    /// <returns>严格晚于 <paramref name="afterUtc"/> 的时刻。</returns>
    public static DateTimeOffset ComputeNextInterval(DateTimeOffset afterUtc, int intervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        return afterUtc.AddSeconds(intervalSeconds);
    }

    /// <summary>
    /// 功能作用：解析 5 段标准 Cron，在指定时区计算下一次运行（不含当前刻）。
    /// </summary>
    /// <param name="afterUtc">基准 UTC 时刻。</param>
    /// <param name="cronExpression">Cron 表达式（分 时 日 月 周）。</param>
    /// <param name="timeZoneId">IANA 或 Windows 时区 Id；空白或 <c>Local</c> 表示本机时区。</param>
    /// <returns>下次运行 UTC；表达式无效或无下次时返回 null。</returns>
    public static DateTimeOffset? TryComputeNextCron(
        DateTimeOffset afterUtc,
        string cronExpression,
        string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        try
        {
            var expr = CronExpression.Parse(cronExpression.Trim(), CronFormat.Standard);
            var zone = ResolveTimeZone(timeZoneId);
            var next = expr.GetNextOccurrence(afterUtc, zone, inclusive: false);
            return next;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 功能作用：按触发方式计算初始下次运行时刻。Cron 无效或未知类型返回 null。
    /// </summary>
    /// <param name="kind">间隔或 Cron。</param>
    /// <param name="intervalSeconds">间隔秒数；仅间隔模式使用，必须大于 0。</param>
    /// <param name="cronExpression">5 段 Cron；仅 Cron 模式使用。</param>
    /// <param name="timeZoneId">Cron 时区。</param>
    /// <param name="nowUtc">当前时刻。</param>
    /// <returns>下次时刻；无法计算时为 null。</returns>
    public static DateTimeOffset? TryComputeInitialNextRun(
        ScheduleKind kind,
        int intervalSeconds,
        string? cronExpression,
        string? timeZoneId,
        DateTimeOffset nowUtc)
    {
        return kind switch
        {
            ScheduleKind.Interval => ComputeNextInterval(nowUtc, intervalSeconds),
            ScheduleKind.Cron => TryComputeNextCron(nowUtc, cronExpression ?? string.Empty, timeZoneId),
            _ => null
        };
    }

    /// <summary>
    /// 功能作用：在一次触发后推进下次运行时刻。
    /// 间隔模式以计划时刻锚定推进；计划时刻加间隔仍不晚于实际触发时刻时，从实际触发时刻重新起算。
    /// Cron 模式按实际触发时刻取下一次；无效或永无下次返回 null。
    /// </summary>
    /// <param name="kind">间隔或 Cron。</param>
    /// <param name="intervalSeconds">间隔秒数；仅间隔模式使用，必须大于 0。</param>
    /// <param name="cronExpression">5 段 Cron。</param>
    /// <param name="timeZoneId">Cron 时区。</param>
    /// <param name="firedAtUtc">实际触发时刻。</param>
    /// <param name="scheduledAtUtc">本次计划时刻。</param>
    /// <returns>下次时刻；无法计算时为 null。</returns>
    public static DateTimeOffset? TryComputeNextAfterFire(
        ScheduleKind kind,
        int intervalSeconds,
        string? cronExpression,
        string? timeZoneId,
        DateTimeOffset firedAtUtc,
        DateTimeOffset scheduledAtUtc)
    {
        return kind switch
        {
            ScheduleKind.Interval => ComputeAnchoredInterval(firedAtUtc, scheduledAtUtc, intervalSeconds),
            ScheduleKind.Cron => TryComputeNextCron(firedAtUtc, cronExpression ?? string.Empty, timeZoneId),
            _ => null
        };
    }

    /// <summary>
    /// 功能作用：间隔模式锚定推进：下次 = 计划时刻 + 间隔；若仍在过去（落后过多）则从触发时刻起算，避免追帧连发。
    /// </summary>
    private static DateTimeOffset ComputeAnchoredInterval(
        DateTimeOffset firedAtUtc,
        DateTimeOffset scheduledAtUtc,
        int intervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        var anchored = scheduledAtUtc.AddSeconds(intervalSeconds);
        return anchored > firedAtUtc ? anchored : firedAtUtc.AddSeconds(intervalSeconds);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || string.Equals(timeZoneId, "Local", StringComparison.OrdinalIgnoreCase))
            return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }
}

/// <summary>定时触发方式：固定间隔或 5 段 Cron。</summary>
public enum ScheduleKind
{
    Interval = 0,
    Cron = 1
}
