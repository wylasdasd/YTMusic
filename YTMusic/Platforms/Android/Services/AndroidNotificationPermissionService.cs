using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace YTMusic.Platforms.Android.Services
{
    public class AndroidNotificationPermissionService : YTMusic.Services.INotificationPermissionService
    {
        public async Task<bool> IsGrantedAsync()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return true;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }

        public async Task<bool> RequestAsync()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return true;
            }

            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }

        public Task OpenSettingsAsync()
        {
            AppInfo.Current.ShowSettingsUI();
            return Task.CompletedTask;
        }
    }
}
