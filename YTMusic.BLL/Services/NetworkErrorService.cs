using YTMusic.BLL.Abstractions;

namespace YTMusic.BLL.Services;

public sealed class NetworkErrorService : INetworkErrorService
{
    public event Action<string>? NotificationRequested;

    public Task NotifyFailureAsync(string context, Exception? ex = null)
    {
        if (!ShouldNotify())
        {
            return Task.CompletedTask;
        }

        NotificationRequested?.Invoke(NetworkErrorHelper.FormatMessage(context, ex));
        return Task.CompletedTask;
    }

    private static bool ShouldNotify()
    {
        lock (AppGlobal.Runtime.NetworkErrorNotifyLock)
        {
            if (DateTime.UtcNow - AppGlobal.Runtime.LastNetworkErrorNotifiedUtc < AppGlobal.Network.ErrorNotifyCooldown)
            {
                return false;
            }

            AppGlobal.Runtime.LastNetworkErrorNotifiedUtc = DateTime.UtcNow;
            return true;
        }
    }
}
