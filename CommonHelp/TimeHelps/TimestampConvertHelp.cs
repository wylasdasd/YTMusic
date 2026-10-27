using System.Collections.Concurrent;
using System.Globalization;

namespace CommonHelp;

/// <summary>
/// 功能职责：时区下拉选项（时区 Id 与展示标签）。
/// </summary>
public sealed record TimestampTimeZoneOption(string Id, string Label);

/// <summary>
/// 功能职责：时间戳展示结果（秒/毫秒戳、本地/UTC、ISO 8601、相对时间提示）。
/// </summary>
public sealed record TimestampDisplayResult(
    string UnixSeconds,
    string UnixMilliseconds,
    string LocalTime,
    string UtcTime,
    string Iso8601Local,
    string Iso8601Utc,
    string RelativeHint);

/// <summary>
/// 功能职责：Unix 时间戳与日期时间互转。支持秒/毫秒识别、任意时区显示、ISO 8601 解析与相对时间提示。不负责 UI 控件。
/// 方法群组：解析 Unix、解析日期时间、构建展示、时区标签。
/// </summary>
public static class TimestampConvertHelp
{
    private const string DisplayFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>时区选项列表：常用时区置顶（带 ★），其余按 UTC 偏移排序。</summary>
    public static IReadOnlyList<TimestampTimeZoneOption> TimeZoneOptions { get; } = BuildTimeZoneOptions();

    /// <summary>
    /// 功能作用：解析 Unix 秒/毫秒时间戳（≥12 位按毫秒）。按 preferField 优先取值，失败时回退另一字段。
    /// </summary>
    /// <param name="unixSeconds">秒字段文本。</param>
    /// <param name="unixMilliseconds">毫秒字段文本。</param>
    /// <param name="preferField">优先使用的字段。</param>
    /// <returns>成功时为 DateTimeOffset；空或非法则 Fail。</returns>
    public static Result<DateTimeOffset> ParseUnix(string? unixSeconds, string? unixMilliseconds, UnixFieldPrefer preferField)
    {
        if (string.IsNullOrWhiteSpace(unixSeconds) && string.IsNullOrWhiteSpace(unixMilliseconds))
            return Result<DateTimeOffset>.Fail("empty");

        var secText = (unixSeconds ?? string.Empty).Trim();
        var msText = (unixMilliseconds ?? string.Empty).Trim();

        if (preferField == UnixFieldPrefer.Seconds && TryParseUnixToken(secText, out var dto))
            return Result<DateTimeOffset>.Ok(dto);
        if (preferField == UnixFieldPrefer.Milliseconds && TryParseUnixToken(msText, out dto))
            return Result<DateTimeOffset>.Ok(dto);
        if (TryParseUnixToken(secText, out dto))
            return Result<DateTimeOffset>.Ok(dto);
        if (TryParseUnixToken(msText, out dto))
            return Result<DateTimeOffset>.Ok(dto);

        return Result<DateTimeOffset>.Fail("invalid");
    }

    /// <summary>
    /// 功能作用：ParseUnix 的简化重载，优先秒字段。
    /// </summary>
    /// <param name="unixSeconds">秒字段文本。</param>
    /// <param name="unixMilliseconds">毫秒字段文本。</param>
    /// <returns>成功时为 DateTimeOffset；空或非法则 Fail。</returns>
    public static Result<DateTimeOffset> ParseUnix(string? unixSeconds, string? unixMilliseconds) =>
        ParseUnix(unixSeconds, unixMilliseconds, UnixFieldPrefer.Seconds);

    /// <summary>
    /// 功能作用：由时区时间与/或 UTC 时间（支持 ISO 8601）解析 DateTimeOffset；两者同时提供时校验偏差不超过 1 分钟。
    /// </summary>
    /// <param name="localTime">指定时区的墙上时间。</param>
    /// <param name="utcTime">UTC 时间。</param>
    /// <param name="timeZoneId">时区 Id。</param>
    /// <returns>成功时为 DateTimeOffset；格式错误则 Fail。</returns>
    public static Result<DateTimeOffset> ParseFromTime(string? localTime, string? utcTime, string timeZoneId)
    {
        var hasLocal = !string.IsNullOrWhiteSpace(localTime);
        var hasUtc = !string.IsNullOrWhiteSpace(utcTime);

        if (!hasLocal && !hasUtc)
            return Result<DateTimeOffset>.Fail("请填写时区时间或 UTC 时间（支持 ISO 8601）。");

        DateTimeOffset dto;
        if (hasLocal && hasUtc)
        {
            if (!TryParseLocalTimeInZone(localTime!, timeZoneId, out dto))
                return Result<DateTimeOffset>.Fail("时区时间格式不正确。");

            if (!TryParseDateTimeInput(utcTime!, preferUtc: true, out var utcDto))
                return Result<DateTimeOffset>.Fail("UTC 时间格式不正确。");

            if (Math.Abs(dto.ToUnixTimeMilliseconds() - utcDto.ToUnixTimeMilliseconds()) > 60_000)
                return Result<DateTimeOffset>.Fail("时区时间与 UTC 相差超过 1 分钟，请检查输入。");
        }
        else if (hasLocal)
        {
            if (!TryParseLocalTimeInZone(localTime!, timeZoneId, out dto))
                return Result<DateTimeOffset>.Fail("时区时间格式不正确。");
        }
        else
        {
            if (!TryParseDateTimeInput(utcTime!, preferUtc: true, out dto))
                return Result<DateTimeOffset>.Fail("UTC 时间格式不正确。");
        }

        return Result<DateTimeOffset>.Ok(dto);
    }

    /// <summary>
    /// 功能作用：构建指定时区下的完整展示（秒/毫秒戳、本地/UTC、ISO 8601、相对时间提示）。
    /// </summary>
    /// <param name="dto">已解析的时间。</param>
    /// <param name="timeZoneId">展示用时区 Id。</param>
    /// <returns>展示字段集合。</returns>
    public static TimestampDisplayResult BuildDisplay(DateTimeOffset dto, string timeZoneId)
    {
        var tz = FindTimeZone(timeZoneId);
        var zoned = TimeZoneInfo.ConvertTimeFromUtc(dto.UtcDateTime, tz);
        var offset = tz.GetUtcOffset(zoned);
        var zonedOffset = new DateTimeOffset(zoned, offset);

        return new TimestampDisplayResult(
            dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            zoned.ToString(DisplayFormat, CultureInfo.InvariantCulture),
            dto.UtcDateTime.ToString(DisplayFormat, CultureInfo.InvariantCulture),
            zonedOffset.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            FormatRelative(dto, zoned));
    }

    /// <summary>
    /// 功能作用：生成时区展示标签（如 China Standard Time (UTC+08:00)）。
    /// </summary>
    /// <param name="timeZoneId">时区 Id。</param>
    /// <returns>展示标签。</returns>
    public static string FormatLocalTimeLabel(string timeZoneId)
    {
        var tz = FindTimeZone(timeZoneId);
        var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{tz.DisplayName} ({FormatUtcOffset(offset)})";
    }

    /// <summary>
    /// 功能作用：尝试把单个时间戳文本解析为 DateTimeOffset。纯数字 ≥12 位按毫秒，否则按秒；非法或越界返回 false。
    /// </summary>
    /// <param name="text">时间戳文本。</param>
    /// <param name="dto">解析成功时的时间。</param>
    /// <returns>解析成功为 true。</returns>
    public static bool TryParseUnixToken(string text, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;

        var digits = text.TrimStart('-').Length;
        try
        {
            dto = digits >= 12
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 在指定时区内解析无时区标记的本地时间（视为该时区的墙上时间）。
    /// </summary>
    private static bool TryParseLocalTimeInZone(string value, string timeZoneId, out DateTimeOffset dto)
    {
        dto = default;
        if (!TryParseDateTimeOnly(value, out var dt))
            return false;

        var tz = FindTimeZone(timeZoneId);
        dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);

        try
        {
            var offset = tz.GetUtcOffset(dt);
            dto = new DateTimeOffset(dt, offset);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 解析 DateTime：先用当前文化，失败再退化到 InvariantCulture。
    /// </summary>
    private static bool TryParseDateTimeOnly(string value, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces;

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, styles, out dt))
            return true;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, styles, out dt);
    }

    /// <summary>
    /// 解析 DateTimeOffset：preferUtc 或输入以 Z 结尾时按 UTC 解析，否则按本地时间。
    /// </summary>
    private static bool TryParseDateTimeInput(string value, bool preferUtc, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal;
        if (preferUtc || value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, styles, out dto))
            return true;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, styles, out dto);
    }

    /// <summary>已解析的时区缓存；TimeZoneInfo 不可变且线程安全，缓存可避免每次查找重建实例。</summary>
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> TimeZoneCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 按 Id 查找时区（带缓存）；找不到时回退到系统本地时区。
    /// </summary>
    private static TimeZoneInfo FindTimeZone(string timeZoneId) =>
        TimeZoneCache.GetOrAdd(timeZoneId, static id =>
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
        });

    /// <summary>
    /// 生成相对当前时间的提示文案（x 天/小时/分钟/秒 之前或之后）。
    /// </summary>
    private static string FormatRelative(DateTimeOffset dto, DateTime zoned)
    {
        var delta = dto - DateTimeOffset.UtcNow;
        var abs = delta.Duration();

        if (abs.TotalSeconds < 1)
            return "与当前时间几乎相同";

        var future = delta.TotalSeconds > 0;
        var prefix = future ? "之后" : "之前";

        if (abs.TotalDays >= 1)
            return $"{abs.TotalDays:0.#} 天{prefix}（{zoned:dddd}）";
        if (abs.TotalHours >= 1)
            return $"{abs.TotalHours:0.#} 小时{prefix}";
        if (abs.TotalMinutes >= 1)
            return $"{abs.TotalMinutes:0.#} 分钟{prefix}";
        return $"{abs.TotalSeconds:0.#} 秒{prefix}";
    }

    /// <summary>
    /// 构建时区选项列表：常用时区（本地/UTC/中国/日本/欧洲/美国东西海岸等）置顶，其余系统时区按偏移排序追加。
    /// </summary>
    private static IReadOnlyList<TimestampTimeZoneOption> BuildTimeZoneOptions()
    {
        var preferredIds = new[]
        {
            TimeZoneInfo.Local.Id,
            TimeZoneInfo.Utc.Id,
            "China Standard Time",
            "Asia/Shanghai",
            "Tokyo Standard Time",
            "Asia/Tokyo",
            "GMT Standard Time",
            "Europe/London",
            "Central European Standard Time",
            "Europe/Berlin",
            "Eastern Standard Time",
            "America/New_York",
            "Pacific Standard Time",
            "America/Los_Angeles",
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new List<TimestampTimeZoneOption>();

        foreach (var id in preferredIds)
            TryAddTimeZone(id, pin: true);

        var rest = TimeZoneInfo.GetSystemTimeZones()
            .Where(tz => seen.Add(tz.Id))
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tz => CreateOption(tz, pin: false));

        options.AddRange(rest);
        return options;

        void TryAddTimeZone(string timeZoneId, bool pin)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                if (!seen.Add(tz.Id))
                    return;

                options.Add(CreateOption(tz, pin));
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }
    }

    /// <summary>
    /// 由 TimeZoneInfo 创建下拉选项；pin 为 true 时标签前加 ★ 标记常用。
    /// </summary>
    private static TimestampTimeZoneOption CreateOption(TimeZoneInfo tz, bool pin)
    {
        var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
        var prefix = pin ? "★ " : string.Empty;
        var label = $"{prefix}{tz.DisplayName} ({FormatUtcOffset(offset)}) · {tz.Id}";
        return new TimestampTimeZoneOption(tz.Id, label);
    }

    /// <summary>
    /// 格式化 UTC 偏移为 +HH:mm / -HH:mm。
    /// </summary>
    private static string FormatUtcOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var abs = offset.Duration();
        return $"UTC{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }
}

/// <summary>
/// 功能职责：时间戳解析时优先使用的字段（秒或毫秒）。
/// </summary>
public enum UnixFieldPrefer
{
    /// <summary>优先秒字段。</summary>
    Seconds,

    /// <summary>优先毫秒字段。</summary>
    Milliseconds
}
