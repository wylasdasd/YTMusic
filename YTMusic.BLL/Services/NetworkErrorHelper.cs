using System.Net.Http;
using YTMusic.BLL;

namespace YTMusic.BLL.Services;
public static class NetworkErrorHelper
{
    public static string FormatMessage(string context, Exception? ex)
    {
        var detail = string.IsNullOrWhiteSpace(ex?.Message)
            ? AppGlobal.Network.DefaultConnectionError
            : ex.Message.Trim();

        return $"{context}失败: {detail}。{AppGlobal.Network.VpnRetryHint}";
    }

    public static bool IsNetworkRelated(Exception? ex)
    {
        if (ex == null)
        {
            return false;
        }

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
