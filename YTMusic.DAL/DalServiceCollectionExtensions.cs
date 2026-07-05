using Microsoft.Extensions.DependencyInjection;
using YTMusic.BLL.Abstractions.Data;
using YTMusic.DAL.Repositories;

namespace YTMusic.DAL;

public static class DalServiceCollectionExtensions
{
    public static IServiceCollection AddYTMusicDal(this IServiceCollection services)
    {
        services.AddSingleton<IFavoriteRepository, FavoriteRepository>();
        services.AddSingleton<IDownloadedTrackRepository, DownloadedTrackRepository>();
        return services;
    }
}
