#if WINDOWS
using WinRT.Interop;
using Windows.Storage.Pickers;
#endif
using YTMusic.BLL.Ports;

namespace YTMusic.Adapters;

public sealed class MauiFolderPicker : IFolderPicker
{
    public async Task<FolderPickResult> PickAsync(string title)
    {
#if WINDOWS
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
        {
            return new FolderPickResult { IsSupported = true };
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(winUiWindow));
        var folder = await picker.PickSingleFolderAsync();
        return new FolderPickResult
        {
            IsSupported = true,
            Path = folder?.Path
        };
#else
        await Task.CompletedTask;
        return new FolderPickResult { IsSupported = false };
#endif
    }
}
