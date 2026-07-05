namespace YTMusic.BLL.Abstractions;

public interface INetworkErrorService
{
    event Action<string>? NotificationRequested;

    Task NotifyFailureAsync(string context, Exception? ex = null);
}
