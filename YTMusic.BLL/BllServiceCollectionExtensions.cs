using Microsoft.Extensions.DependencyInjection;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Infrastructure.AList;
using YTMusic.BLL.Infrastructure.FileSystem;
using YTMusic.BLL.Infrastructure.YouTube;
using YTMusic.BLL.Ports;
using YTMusic.BLL.Services;

namespace YTMusic.BLL;

public static class BllServiceCollectionExtensions
{
    public static IServiceCollection AddYTMusicBll(this IServiceCollection services)
    {
        services.AddSingleton<IYouTubeApiClient, YoutubeExplodeClient>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<AListFsApiClient>();

        services.AddSingleton<INetworkErrorService, NetworkErrorService>();
        services.AddSingleton<IYouTubeService, YouTubeService>();
        services.AddSingleton<IAListUploadSettingsService, AListUploadSettingsService>();
        services.AddSingleton<IAListUploadService, AListUploadService>();
        services.AddSingleton<IFavoriteService, FavoriteService>();
        services.AddSingleton<ILocalMusicService, LocalMusicService>();
        services.AddSingleton<IDownloadManagerService, DownloadManagerService>();
        services.AddSingleton<IUploadManagerService, UploadManagerService>();
        services.AddSingleton<IAListRemoteDownloadManagerService, AListRemoteDownloadManagerService>();
        return services;
    }
}
