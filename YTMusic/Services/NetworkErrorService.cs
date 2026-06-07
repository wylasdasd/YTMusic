using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace YTMusic.Services
{
    public class NetworkErrorService
    {
        private static DateTime s_lastNotified = DateTime.MinValue;
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);
        private static readonly object s_lock = new();

        /// <summary>
        /// 由 MainLayout 订阅，在 UI 线程显示 Snackbar。
        /// </summary>
        public event Action<string>? SnackbarRequested;

        public Task NotifyFailureAsync(string context, Exception? ex = null)
        {
            if (!ShouldNotify())
                return Task.CompletedTask;

            SnackbarRequested?.Invoke(FormatMessage(context, ex));
            return Task.CompletedTask;
        }

        public static string FormatMessage(string context, Exception? ex)
        {
            var detail = string.IsNullOrWhiteSpace(ex?.Message)
                ? "无法连接到 YouTube 服务器"
                : ex.Message.Trim();

            return $"{context}失败: {detail}。可尝试切换 VPN 节点后重试。";
        }

        private static bool ShouldNotify()
        {
            lock (s_lock)
            {
                if (DateTime.UtcNow - s_lastNotified < Cooldown)
                    return false;

                s_lastNotified = DateTime.UtcNow;
                return true;
            }
        }

        public static bool IsNetworkRelated(Exception? ex)
        {
            if (ex == null)
                return false;

            return ex is HttpRequestException
                || ex is TaskCanceledException
                || ex is OperationCanceledException
                || ex is System.Net.Sockets.SocketException
                || ex is System.Net.WebException
                || (ex is IOException && ex.InnerException is System.Net.Sockets.SocketException)
                || ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("DNS", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("connect", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("unreachable", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("连接", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("主机", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("youtube.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
