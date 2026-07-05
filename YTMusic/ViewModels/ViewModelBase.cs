namespace YTMusic.ViewModels;

public abstract class ViewModelBase
{
    public Action? StateHasChanged { get; set; }

    protected void NotifyChanged() => StateHasChanged?.Invoke();
}
