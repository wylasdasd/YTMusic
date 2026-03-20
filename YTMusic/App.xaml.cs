#if WINDOWS
using YTMusic.ForWindows.Windows;
#endif

namespace YTMusic
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
#if WINDOWS
            return new MainWindow();
#else
            return new Window(new MainPage()) { Title = "YTMusic" };
#endif
        }
    }
}
