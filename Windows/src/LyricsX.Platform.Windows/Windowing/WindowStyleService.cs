using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Windowing;

/// <summary>
/// Applies the non-activating tool-window style and window-placements for the
/// lyric surface. Pure Win32 style logic, seam-injected for tests; the WinUI
/// host (<see cref="TaskbarLyricsWindowHost"/>) marshals calls to it on the
/// dispatcher queue.
/// </summary>
public sealed class WindowStyleService(IWindowApi windowApi)
{
    public void ApplyNonActivatingStyle(nint hwnd)
    {
        var style = windowApi.GetExStyle(hwnd);
        style |= NativeWindowStyles.WS_EX_TOOLWINDOW | NativeWindowStyles.WS_EX_NOACTIVATE;
        style &= ~NativeWindowStyles.WS_EX_APPWINDOW;
        windowApi.SetExStyle(hwnd, style);
    }

    public void ShowAt(nint hwnd, TaskbarPlacement placement)
    {
        if (!placement.IsVisible)
        {
            Hide(hwnd);
            return;
        }

        windowApi.SetWindowPos(
            hwnd,
            NativeWindowStyles.HWND_TOPMOST,
            placement.Bounds.X,
            placement.Bounds.Y,
            placement.Bounds.Width,
            placement.Bounds.Height,
            NativeWindowStyles.SWP_NOACTIVATE |
            NativeWindowStyles.SWP_SHOWWINDOW);
    }

    public void Hide(nint hwnd) => windowApi.ShowWindow(hwnd, NativeWindowStyles.SW_HIDE);

    /// <summary>
    /// WM_MOUSEACTIVATE result: clicking the lyrics never steals focus.
    /// </summary>
    public static int HandleMouseActivate() => NativeWindowStyles.MA_NOACTIVATE;
}
