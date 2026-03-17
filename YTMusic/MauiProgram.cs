using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using YTMusic.Services;

namespace YTMusic
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            builder.Services.AddSingleton<IYouTubeService, YouTubeService>();
            builder.Services.AddSingleton<ILocalMusicService, LocalMusicService>();
            builder.Services.AddSingleton<GlobalStateService>();
            builder.Services.AddSingleton<IFavoriteService, FavoriteService>();
            builder.Services.AddSingleton<IDownloadManagerService, DownloadManagerService>();
            builder.Services.AddSingleton<MusicPlayerService>();
#if ANDROID
            builder.Services.AddSingleton<INativeAudioPlaybackService, YTMusic.Platforms.Android.Services.AndroidNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INotificationPermissionService, YTMusic.Platforms.Android.Services.AndroidNotificationPermissionService>();
#elif IOS
            builder.Services.AddSingleton<INativeAudioPlaybackService, YTMusic.Platforms.iOS.Services.IosNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INotificationPermissionService, NullNotificationPermissionService>();
#else
            builder.Services.AddSingleton<INativeAudioPlaybackService, NullNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INotificationPermissionService, NullNotificationPermissionService>();
#endif
            builder.Services.AddScoped<YTMusic.Components.Pages.SearchVM>();
            builder.Services.AddTransient<YTMusic.Components.Pages.DownloadsVM>();
            builder.Services.AddTransient<YTMusic.Components.Pages.TransfersVM>();
            builder.Services.AddTransient<YTMusic.Components.Pages.FavoritesVM>();
            builder.Services.AddMudServices();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
