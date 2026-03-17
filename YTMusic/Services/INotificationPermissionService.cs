using System.Threading.Tasks;

namespace YTMusic.Services
{
    public interface INotificationPermissionService
    {
        Task<bool> IsGrantedAsync();
        Task<bool> RequestAsync();
        Task OpenSettingsAsync();
    }
}
