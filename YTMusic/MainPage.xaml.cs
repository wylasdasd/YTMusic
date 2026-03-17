using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace YTMusic
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BackgroundColor = Color.FromArgb("#0D1017");
            ApplyTopSafeArea();
        }

        private void ApplyTopSafeArea()
        {
#if IOS
            // Let iOS apply notch/status-bar safe area automatically.
            On<iOS>().SetUseSafeArea(true);
#elif ANDROID
            // Android WebView may not expose CSS safe-area vars reliably,
            // so we pad the native page by status bar height.
            var statusBarTop = GetAndroidStatusBarHeight();
            Padding = new Thickness(0, statusBarTop, 0, 0);
#endif
        }

#if ANDROID
        private static double GetAndroidStatusBarHeight()
        {
            var context = Android.App.Application.Context;
            var resources = context?.Resources;
            if (resources is null)
            {
                return 0;
            }

            var resourceId = resources.GetIdentifier("status_bar_height", "dimen", "android");
            if (resourceId <= 0)
            {
                return 0;
            }

            var pixels = resources.GetDimensionPixelSize(resourceId);
            return pixels / DeviceDisplay.MainDisplayInfo.Density;
        }
#endif
    }
}
