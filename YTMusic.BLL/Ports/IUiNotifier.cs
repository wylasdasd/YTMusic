namespace YTMusic.BLL.Ports;

public interface IUiNotifier
{
    void Info(string message);
    void Success(string message);
    void Warning(string message);
    void Error(string message);
}
