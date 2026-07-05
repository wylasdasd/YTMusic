using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using YTMusic.Adapters;
using YTMusic.BLL;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.DAL;
using YTMusic.ViewModels;
using YTMusic.Services;
using YTMusic.Services.Abstractions;

#if WINDOWS
using Microsoft.UI.Windowing;
#endif

namespace YTMusic
{
    public static class MauiProgram
    {
        public static IServiceProvider? Services
        {
            get => AppGlobal.Runtime.Services;
            private set => AppGlobal.Runtime.Services = value;
        }

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
            builder.Services.AddMudServices();

            builder.Services.AddScoped<IUiNotifier, MudUiNotifier>();
            builder.Services.AddScoped<IDialogHost, MudDialogHost>();
            builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();

            builder.Services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();
            builder.Services.AddSingleton<IDatabasePathProvider, MauiDatabasePathProvider>();
            builder.Services.AddSingleton<IDownloadMusicDirectoryProvider, MauiDownloadMusicDirectoryProvider>();
            builder.Services.AddYTMusicDal();
            builder.Services.AddYTMusicBll();

            builder.Services.AddSingleton<GlobalStateService>();
            builder.Services.AddSingleton<UiPreferencesService>();
            builder.Services.AddSingleton<AppResetService>();
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
            builder.Services.AddScoped<SearchVM>();
            builder.Services.AddScoped<DownloadsVM>();
            builder.Services.AddScoped<TransfersVM>();
            builder.Services.AddScoped<FavoritesVM>();
            builder.Services.AddScoped<FavoritesFolderVM>();
            builder.Services.AddScoped<UploadVM>();

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
