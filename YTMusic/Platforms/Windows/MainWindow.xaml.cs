namespace YTMusic.Platforms.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "YTMusic";
            Width = 1200;
            Height = 800;
            Page = new MainPage();
            TitleBar = new TitleBar
            {
                BackgroundColor = Color.FromArgb("#5E35B1"),
                ForegroundColor = Color.FromArgb("#5E35B1"),
                HeightRequest = 0
            };
        }
    }
}
