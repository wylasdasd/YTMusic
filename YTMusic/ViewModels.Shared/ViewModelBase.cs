using CommunityToolkit.Mvvm.ComponentModel;

namespace YTMusic.ViewModels.Shared;

public abstract class ViewModelBase : ObservableObject
{
    public Action? StateHasChanged { get; set; }

    protected ViewModelBase()
    {
        PropertyChanged += (_, _) => StateHasChanged?.Invoke();
    }

    protected void NotifyChanged() => StateHasChanged?.Invoke();
}
