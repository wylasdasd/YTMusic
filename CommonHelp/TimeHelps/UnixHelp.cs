namespace CommonTool.TimeHelps
{
    public static class UnixHelp // 建议设为 static 类，因为里面全是静态方法
    {
        /// <summary>
        /// 获取当前的 Unix 时间戳（秒）。
        /// </summary>
        public static long GetUtcNowUnixTimeSeconds()
            => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// 获取当前的 Unix 时间戳（毫秒）。
        /// </summary>
        public static long GetUtcNowUnixTimeMilliseconds()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// 格里尼治时间
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static long GetUtcNowUnixTimeMilliseconds(this DateTime dateTime)
        {
            DateTimeOffset dto = new DateTimeOffset(dateTime);
            long unixMilliseconds = dto.ToUnixTimeMilliseconds();
            return unixMilliseconds;
        }

        /// <summary>
        /// 秒级 Unix 时间戳转换成本地 DateTime。
        /// </summary>
        public static DateTime ConvertTimestampToDateTime(long timestamp)
        {
            // 1. 先从秒戳转为 DateTimeOffset (它是绝对时间，不带歧义)
            // 2. 然后直接转为本地时间
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
        }

        /// <summary>
        /// 毫秒级 Unix 时间戳转换成本地 DateTime。
        /// </summary>
        public static DateTime ConvertMsTimestampToDateTime(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
        }
    }

    public interface ITimeService
    {
        long GetUnixTimeSeconds();

        long GetUnixTimeMilliseconds();

        DateTime GetLocalDateTime();

        DateTime ConvertToLocal(long unixSeconds);
    }

    // 使用 .NET 8/10 的主构造函数语法 (Primary Constructor)
    public class TimeService : ITimeService
    {
        private readonly TimeProvider timeProvider;

        public TimeService(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        // 获取当前 Unix 秒
        public long GetUnixTimeSeconds()
            => timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // 获取当前 Unix 毫秒
        public long GetUnixTimeMilliseconds()
            => timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        // 获取当前本地时间
        public DateTime GetLocalDateTime()
            => timeProvider.GetLocalNow().DateTime;

        // 将外部传入的秒戳转为本地时间
        public DateTime ConvertToLocal(long unixSeconds)
            => DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                               .ToOffset(timeProvider.LocalTimeZone.GetUtcOffset(DateTimeOffset.UtcNow))
                               .DateTime;
    }
}