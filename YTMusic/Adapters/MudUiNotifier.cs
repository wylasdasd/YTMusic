using MudBlazor;
using YTMusic.BLL.Ports;

namespace YTMusic.Adapters;

public sealed class MudUiNotifier(ISnackbar snackbar) : IUiNotifier
{
    public void Info(string message) => snackbar.Add(message, Severity.Info);

    public void Success(string message) => snackbar.Add(message, Severity.Success);

    public void Warning(string message) => snackbar.Add(message, Severity.Warning);

    public void Error(string message) => snackbar.Add(message, Severity.Error);
}
