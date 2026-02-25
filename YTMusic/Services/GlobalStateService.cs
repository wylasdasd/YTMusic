using System;

namespace YTMusic.Services
{
    public class GlobalStateService
    {
        public bool IsLoading { get; private set; }
        public event Action? OnChange;

        public void ShowLoading()
        {
            IsLoading = true;
            NotifyStateChanged();
        }

        public void HideLoading()
        {
            IsLoading = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
