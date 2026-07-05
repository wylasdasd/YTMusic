using MudBlazor;

namespace YTMusic.ViewModels;

public static class ThemePresets
{
    public static ThemePreset[] All =
    [
        new(
            "Neon Night",
            "#FF4E6A",
            true,
            new MudTheme
            {
                PaletteDark = new PaletteDark
                {
                    Primary = "#FF4E6A",
                    Secondary = "#7C8DB5",
                    Tertiary = "#F59E0B",
                    Background = "#0D1017",
                    Surface = "#151A26",
                    AppbarBackground = "#0D1017",
                    DrawerBackground = "#111522",
                    TextPrimary = "#F5F7FA",
                    TextSecondary = "#B0BACB",
                    Divider = "rgba(255,255,255,0.08)"
                }
            },
            "--ytm-bg-radial-1: rgba(255, 78, 106, 0.18);" +
            "--ytm-bg-radial-2: rgba(99, 102, 241, 0.15);" +
            "--ytm-bg-base: #0D1017;" +
            "--ytm-topbar-grad-1: rgba(13, 16, 23, 0.95);" +
            "--ytm-topbar-grad-2: rgba(13, 16, 23, 0.82);" +
            "--ytm-border: rgba(255, 255, 255, 0.08);" +
            "--ytm-brand: #F6F8FD;" +
            "--ytm-accent: #FF4E6A;" +
            "--ytm-search-border: rgba(255, 255, 255, 0.14);" +
            "--ytm-search-bg: rgba(255, 255, 255, 0.06);" +
            "--ytm-search-text: #C8D0DE;" +
            "--ytm-bottom-bg: rgba(13, 16, 23, 0.97);" +
            "--ytm-nav-text: #AEB9D2;" +
            "--ytm-nav-active-bg: rgba(255, 78, 106, 0.25);" +
            "--ytm-nav-active-text: #FF4E6A;" +
            "--ytm-text-primary: #F5F7FA;" +
            "--ytm-text-secondary: #B0BACB;"
        ),
        new(
            "Ocean Dark",
            "#2DD4BF",
            true,
            new MudTheme
            {
                PaletteDark = new PaletteDark
                {
                    Primary = "#2DD4BF",
                    Secondary = "#93C5FD",
                    Tertiary = "#F59E0B",
                    Background = "#07131B",
                    Surface = "#0E1E29",
                    AppbarBackground = "#07131B",
                    DrawerBackground = "#0B1822",
                    TextPrimary = "#EAF7FF",
                    TextSecondary = "#9CC2D6",
                    Divider = "rgba(255,255,255,0.10)"
                }
            },
            "--ytm-bg-radial-1: rgba(45, 212, 191, 0.16);" +
            "--ytm-bg-radial-2: rgba(59, 130, 246, 0.15);" +
            "--ytm-bg-base: #07131B;" +
            "--ytm-topbar-grad-1: rgba(7, 19, 27, 0.95);" +
            "--ytm-topbar-grad-2: rgba(7, 19, 27, 0.82);" +
            "--ytm-border: rgba(255, 255, 255, 0.10);" +
            "--ytm-brand: #EAF7FF;" +
            "--ytm-accent: #2DD4BF;" +
            "--ytm-search-border: rgba(255, 255, 255, 0.18);" +
            "--ytm-search-bg: rgba(255, 255, 255, 0.05);" +
            "--ytm-search-text: #B6D9E9;" +
            "--ytm-bottom-bg: rgba(7, 19, 27, 0.97);" +
            "--ytm-nav-text: #A5C4D5;" +
            "--ytm-nav-active-bg: rgba(45, 212, 191, 0.24);" +
            "--ytm-nav-active-text: #2DD4BF;" +
            "--ytm-text-primary: #EAF7FF;" +
            "--ytm-text-secondary: #9CC2D6;"
        ),
        new(
            "Amber Dark",
            "#F59E0B",
            true,
            new MudTheme
            {
                PaletteDark = new PaletteDark
                {
                    Primary = "#F59E0B",
                    Secondary = "#FECACA",
                    Tertiary = "#FB7185",
                    Background = "#14100A",
                    Surface = "#21180F",
                    AppbarBackground = "#14100A",
                    DrawerBackground = "#1A130C",
                    TextPrimary = "#FFF6EA",
                    TextSecondary = "#D8C4AC",
                    Divider = "rgba(255,255,255,0.09)"
                }
            },
            "--ytm-bg-radial-1: rgba(245, 158, 11, 0.17);" +
            "--ytm-bg-radial-2: rgba(251, 113, 133, 0.12);" +
            "--ytm-bg-base: #14100A;" +
            "--ytm-topbar-grad-1: rgba(20, 16, 10, 0.95);" +
            "--ytm-topbar-grad-2: rgba(20, 16, 10, 0.82);" +
            "--ytm-border: rgba(255, 255, 255, 0.09);" +
            "--ytm-brand: #FFF6EA;" +
            "--ytm-accent: #F59E0B;" +
            "--ytm-search-border: rgba(255, 255, 255, 0.17);" +
            "--ytm-search-bg: rgba(255, 255, 255, 0.05);" +
            "--ytm-search-text: #E4D4C1;" +
            "--ytm-bottom-bg: rgba(20, 16, 10, 0.97);" +
            "--ytm-nav-text: #DCC8AF;" +
            "--ytm-nav-active-bg: rgba(245, 158, 11, 0.24);" +
            "--ytm-nav-active-text: #F59E0B;" +
            "--ytm-text-primary: #FFF6EA;" +
            "--ytm-text-secondary: #D8C4AC;"
        ),
        new(
            "Sky Light",
            "#2563EB",
            false,
            new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#2563EB",
                    Secondary = "#0EA5E9",
                    Tertiary = "#F59E0B",
                    Background = "#EAF4FF",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#EAF4FF",
                    DrawerBackground = "#F5F9FF",
                    TextPrimary = "#0F172A",
                    TextSecondary = "#475569",
                    Divider = "rgba(15,23,42,0.12)"
                }
            },
            "--ytm-bg-radial-1: rgba(37, 99, 235, 0.16);" +
            "--ytm-bg-radial-2: rgba(14, 165, 233, 0.14);" +
            "--ytm-bg-base: #EAF4FF;" +
            "--ytm-topbar-grad-1: rgba(234, 244, 255, 0.97);" +
            "--ytm-topbar-grad-2: rgba(234, 244, 255, 0.85);" +
            "--ytm-border: rgba(15, 23, 42, 0.14);" +
            "--ytm-brand: #0F172A;" +
            "--ytm-accent: #2563EB;" +
            "--ytm-search-border: rgba(15, 23, 42, 0.18);" +
            "--ytm-search-bg: rgba(255, 255, 255, 0.88);" +
            "--ytm-search-text: #334155;" +
            "--ytm-bottom-bg: rgba(234, 244, 255, 0.97);" +
            "--ytm-nav-text: #475569;" +
            "--ytm-nav-active-bg: rgba(37, 99, 235, 0.20);" +
            "--ytm-nav-active-text: #2563EB;" +
            "--ytm-text-primary: #0F172A;" +
            "--ytm-text-secondary: #475569;"
        ),
        new(
            "Mint Light",
            "#059669",
            false,
            new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#059669",
                    Secondary = "#06B6D4",
                    Tertiary = "#F59E0B",
                    Background = "#EEFDF7",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#EEFDF7",
                    DrawerBackground = "#F5FFFB",
                    TextPrimary = "#102A23",
                    TextSecondary = "#4B635A",
                    Divider = "rgba(16,42,35,0.13)"
                }
            },
            "--ytm-bg-radial-1: rgba(5, 150, 105, 0.15);" +
            "--ytm-bg-radial-2: rgba(6, 182, 212, 0.13);" +
            "--ytm-bg-base: #EEFDF7;" +
            "--ytm-topbar-grad-1: rgba(238, 253, 247, 0.97);" +
            "--ytm-topbar-grad-2: rgba(238, 253, 247, 0.85);" +
            "--ytm-border: rgba(16, 42, 35, 0.14);" +
            "--ytm-brand: #102A23;" +
            "--ytm-accent: #059669;" +
            "--ytm-search-border: rgba(16, 42, 35, 0.18);" +
            "--ytm-search-bg: rgba(255, 255, 255, 0.90);" +
            "--ytm-search-text: #385148;" +
            "--ytm-bottom-bg: rgba(238, 253, 247, 0.97);" +
            "--ytm-nav-text: #4B635A;" +
            "--ytm-nav-active-bg: rgba(5, 150, 105, 0.21);" +
            "--ytm-nav-active-text: #059669;" +
            "--ytm-text-primary: #102A23;" +
            "--ytm-text-secondary: #4B635A;"
        )
    ];

    public static ThemePreset Fallback = new(
        "Fallback",
        "#111111",
        true,
        new MudTheme
        {
            PaletteDark = new PaletteDark
            {
                Primary = "#111111",
                Secondary = "#0E7490",
                Tertiary = "#F59E0B",
                Background = "#0D1017",
                Surface = "#151A26",
                AppbarBackground = "#0D1017",
                DrawerBackground = "#111522",
                TextPrimary = "#F5F7FA",
                TextSecondary = "#B0BACB",
                Divider = "rgba(255,255,255,0.08)"
            }
        },
        "--ytm-bg-radial-1: rgba(255, 78, 106, 0.18);" +
        "--ytm-bg-radial-2: rgba(99, 102, 241, 0.15);" +
        "--ytm-bg-base: #0D1017;" +
        "--ytm-topbar-grad-1: rgba(13, 16, 23, 0.95);" +
        "--ytm-topbar-grad-2: rgba(13, 16, 23, 0.82);" +
        "--ytm-border: rgba(255, 255, 255, 0.08);" +
        "--ytm-brand: #F6F8FD;" +
        "--ytm-accent: #FF4E6A;" +
        "--ytm-search-border: rgba(255, 255, 255, 0.14);" +
        "--ytm-search-bg: rgba(255, 255, 255, 0.06);" +
        "--ytm-search-text: #C8D0DE;" +
        "--ytm-bottom-bg: rgba(13, 16, 23, 0.97);" +
        "--ytm-nav-text: #AEB9D2;" +
        "--ytm-nav-active-bg: rgba(255, 78, 106, 0.25);" +
        "--ytm-nav-active-text: #FF4E6A;" +
        "--ytm-text-primary: #F5F7FA;" +
        "--ytm-text-secondary: #B0BACB;"
    );


    public sealed record ThemePreset(string Name, string PreviewColor, bool IsDark, MudTheme Theme, string CssVars);
}
