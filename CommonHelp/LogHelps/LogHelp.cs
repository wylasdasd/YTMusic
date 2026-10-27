using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace CommonHelp;

/// <summary>
/// 功能职责：Serilog 全局日志装配——拼装「按天滚动文件为主、控制台 / Debug 可选」的 Logger 并桥接 Microsoft.Extensions.Logging；不负责业务日志埋点，也不负责 DI 注册（宿主自行 <c>builder.Logging.AddSerilog(...)</c>）。
/// 方法群组：装配（CreateFileLogger：创建 Logger 返回给调用方，不改动静态 Log.Logger）、桥接（CreateLoggerFactory：把当前 Log.Logger 包成 MEL 工厂，供无 DI 容器场景）。
/// </summary>
public static class LogHelp
{
    /// <summary>默认输出模板：本地时间 + 三位级别 + SourceContext + 消息 + 异常。</summary>
    public const string LogOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{LEVEL:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 功能作用：装配一个「按天滚动文件」为必配、控制台 / Debug 可选的 Logger；Microsoft / System 命名空间统一降到 <paramref name="frameworkMinimumLevel"/> 降噪。
    /// 输入：<paramref name="logDirectory"/> 日志目录（不存在自动创建）；<paramref name="applicationName"/> 应用名（作文件名前缀，产出 <c>{app}-yyyyMMdd.log</c>）；<paramref name="minimumLevel"/> 应用日志级别；<paramref name="frameworkMinimumLevel"/> 框架命名空间级别；<paramref name="console"/> / <paramref name="debug"/> 是否附加控制台 / 调试器输出；<paramref name="retainedFileCountLimit"/> 日志文件保留个数；<paramref name="configure"/> 追加自定义配置（CreateLogger 前执行）。
    /// 输出：返回创建好的 Logger；不改静态 Log.Logger，由调用方决定赋值（如 <c>Log.Logger = LogHelp.CreateFileLogger(...)</c>）。
    /// 异步 / 延时：同步返回；文件由 Serilog 周期性刷新落盘，进程异常退出最多丢最近一批缓冲。
    /// </summary>
    public static Serilog.ILogger CreateFileLogger(
        string logDirectory,
        string applicationName,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        LogEventLevel frameworkMinimumLevel = LogEventLevel.Warning,
        bool console = false,
        bool debug = false,
        int retainedFileCountLimit = 14,
        Action<LoggerConfiguration>? configure = null)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", frameworkMinimumLevel)
            .MinimumLevel.Override("System", frameworkMinimumLevel)
            .WriteTo.File(
                Path.Combine(logDirectory, $"{applicationName}-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCountLimit,
                outputTemplate: LogOutputTemplate);

        if (console)
            configuration.WriteTo.Console(outputTemplate: LogOutputTemplate);

        if (debug)
            configuration.WriteTo.Debug(outputTemplate: LogOutputTemplate);

        configure?.Invoke(configuration);
        return configuration.CreateLogger();
    }

    /// <summary>
    /// 功能作用：把当前静态 <c>Log.Logger</c> 包成 Microsoft.Extensions.Logging 的工厂，供无 DI 容器场景（工具 / 测试）直接取 <c>ILogger&lt;T&gt;</c>。
    /// 输入：<paramref name="dispose"/> 为 true 时，释放工厂会连带释放 Log.Logger。
    /// 输出：返回 ILoggerFactory；日志走向与全局 Log.Logger 完全一致。
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(bool dispose = false)
        => new SerilogLoggerFactory(dispose: dispose);
}
