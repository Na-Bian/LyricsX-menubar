namespace LyricsX.Platform.Windows.Windowing;

/// <summary>Window-style constants and the low-level window-API seam used by
/// <see cref="WindowStyleService"/> (unit tests inject fakes).</summary>
public static class NativeWindowStyles
{
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const long WS_EX_TOPMOST = 0x00000008;

    public const nint HWND_TOPMOST = -1;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int SW_HIDE = 0;
    public const int SW_SHOWNA = 8;

    public const int MA_NOACTIVATE = 3;
}

/// <summary>Minimal Win32 window-API seam for style application.</summary>
public interface IWindowApi
{
    long GetExStyle(nint hwnd);

    void SetExStyle(nint hwnd, long exStyle);

    void SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    void ShowWindow(nint hwnd, int command);
}
