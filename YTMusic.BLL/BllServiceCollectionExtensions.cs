using Microsoft.Extensions.DependencyInjection;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Services;

namespace YTMusic.BLL;

public static class BllServiceCollectionExtensions
{
    public static IServiceCollection AddYTMusicBll(this IServiceCollection services)
    {
        services.AddSingleton<IFavoriteService, FavoriteService>();
        services.AddSingleton<ILocalMusicService, LocalMusicService>();
        return services;
    }
}
