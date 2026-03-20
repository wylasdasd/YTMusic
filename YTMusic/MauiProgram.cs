using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using YTMusic.Services;

#if WINDOWS
using Microsoft.UI.Windowing;
#endif

namespace YTMusic
{
    public static class MauiProgram
    {
        public static IServiceProvider? Services { get; private set; }

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    events.AddWindows(windows =>
                        windows.OnWindowCreated(window =>
                        {
                            if (window is not MauiWinUIWindow mauiWindow)
                            {
                                return;
                            }

                            mauiWindow.ExtendsContentIntoTitleBar = true;

                            var appWindow = mauiWindow.AppWindow;
                            var titleBar = appWindow.TitleBar;
                            titleBar.ExtendsContentIntoTitleBar = true;
                            titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

                            var overlappedPresenter = OverlappedPresenter.Create();
                            overlappedPresenter.SetBorderAndTitleBar(true, false);
                            overlappedPresenter.IsResizable = true;
                            overlappedPresenter.IsMaximizable = true;
                            overlappedPresenter.IsMinimizable = true;
                            overlappedPresenter.IsAlwaysOnTop = false;
                            overlappedPresenter.PreferredMinimumHeight = 600;
                            overlappedPresenter.PreferredMinimumWidth = 800;
                            overlappedPresenter.IsModal = false;
                            appWindow.SetPresenter(overlappedPresenter);
                        }));
#endif
                });

            builder.Services.AddMauiBlazorWebView();

            builder.Services.AddSingleton<IYouTubeService, YouTubeService>();
            builder.Services.AddSingleton<ILocalMusicService, LocalMusicService>();
            builder.Services.AddSingleton<GlobalStateService>();
            builder.Services.AddSingleton<IFavoriteService, FavoriteService>();
            builder.Services.AddSingleton<IDownloadManagerService, DownloadManagerService>();
            builder.Services.AddSingleton<MusicPlayerService>();
            builder.Services.AddSingleton<WindowChromeService>();
#if ANDROID
            builder.Services.AddSingleton<INativeAudioPlaybackService, YTMusic.Platforms.Android.Services.AndroidNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INativeVideoPlaybackService, YTMusic.Platforms.Android.Services.AndroidNativeVideoPlaybackService>();
#elif IOS
            builder.Services.AddSingleton<INativeAudioPlaybackService, YTMusic.Platforms.iOS.Services.IosNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INativeVideoPlaybackService, NullNativeVideoPlaybackService>();
#else
            builder.Services.AddSingleton<INativeAudioPlaybackService, NullNativeAudioPlaybackService>();
            builder.Services.AddSingleton<INativeVideoPlaybackService, NullNativeVideoPlaybackService>();
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

            var app = builder.Build();
            Services = app.Services;
            return app;
        }
    }
}
