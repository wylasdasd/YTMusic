using System.Runtime.InteropServices;
#if WINDOWS
using WinRT.Interop;
#endif

namespace YTMusic.Services;

public class WindowChromeService
{
    private double _lastMouseX;
    private double _lastMouseY;
    private double _lastWindowX;
    private double _lastWindowY;

    public void StartWindowMove()
    {
#if WINDOWS
        var (mouseX, mouseY) = GetCursorPosition();
        var position = GetWindowPosition();
        if (position.Left == 0 && position.Top == 0 && mouseX == 0 && mouseY == 0)
        {
            return;
        }

        _lastMouseX = mouseX;
        _lastMouseY = mouseY;
        _lastWindowX = position.Left;
        _lastWindowY = position.Top;
#endif
    }

    public void MoveWindow()
    {
#if WINDOWS
        var hWnd = GetWindowHandle();
        if (hWnd == nint.Zero)
        {
            return;
        }

        var (mouseX, mouseY) = GetCursorPosition();
        var deltaX = mouseX - _lastMouseX;
        var deltaY = mouseY - _lastMouseY;
        var newWindowX = _lastWindowX + deltaX;
        var newWindowY = _lastWindowY + deltaY;

        _ = SetWindowPos(hWnd, nint.Zero, Convert.ToInt32(newWindowX), Convert.ToInt32(newWindowY), 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);

        _lastMouseX = mouseX;
        _lastMouseY = mouseY;
        _lastWindowX = newWindowX;
        _lastWindowY = newWindowY;
#endif
    }

    public void EndWindowMove()
    {
#if WINDOWS
        _lastMouseX = 0;
        _lastMouseY = 0;
        _lastWindowX = 0;
        _lastWindowY = 0;
#endif
    }

    public void MinimizeWindow()
    {
#if WINDOWS
        var hWnd = GetWindowHandle();
        if (hWnd != nint.Zero)
        {
            _ = ShowWindow(hWnd, SwMinimize);
        }
#endif
    }

    public void ToggleMaximizeWindow()
    {
#if WINDOWS
        var hWnd = GetWindowHandle();
        if (hWnd == nint.Zero)
        {
            return;
        }

        _ = ShowWindow(hWnd, IsZoomed(hWnd) ? SwRestore : SwMaximize);
#endif
    }

    public void CloseWindow()
    {
#if WINDOWS
        var hWnd = GetWindowHandle();
        if (hWnd != nint.Zero)
        {
            _ = PostMessage(hWnd, WmClose, 0, 0);
            return;
        }
#endif

        Application.Current?.Quit();
    }

#if WINDOWS
    private static nint GetWindowHandle()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUiWindow)
        {
            return WindowNative.GetWindowHandle(winUiWindow);
        }

        return nint.Zero;
    }

    private static (int Left, int Top, int Right, int Bottom) GetWindowPosition()
    {
        var hWnd = GetWindowHandle();
        if (hWnd == nint.Zero || !GetWindowRect(hWnd, out var rect))
        {
            return (0, 0, 0, 0);
        }

        return (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static (int X, int Y) GetCursorPosition()
    {
        return GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
    }

    private const uint WmClose = 0x0010;

    private const int SwMinimize = 6;
    private const int SwMaximize = 3;
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
#endif
}
