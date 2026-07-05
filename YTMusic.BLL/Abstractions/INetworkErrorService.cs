namespace YTMusic.BLL.Abstractions;

public interface INetworkErrorService
{
    Task NotifyFailureAsync(string context, Exception? ex = null);
}
