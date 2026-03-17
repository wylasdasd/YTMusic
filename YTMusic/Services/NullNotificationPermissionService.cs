using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace YTMusic.Services
{
    public class NullNotificationPermissionService : INotificationPermissionService
    {
        public Task<bool> IsGrantedAsync() => Task.FromResult(true);
        public Task<bool> RequestAsync() => Task.FromResult(true);
        public Task OpenSettingsAsync()
        {
            AppInfo.Current.ShowSettingsUI();
            return Task.CompletedTask;
        }
    }
}
