using Microsoft.UI.Xaml;
using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Windowing;

/// <summary>Real Win32 window-API adapter.</summary>
public sealed class Win32WindowApi : IWindowApi
{
    public long GetExStyle(nint hwnd) =>
        NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

    public void SetExStyle(nint hwnd, long exStyle) =>
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new nint(exStyle));

    public void SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags) =>
        _ = NativeMethods.SetWindowPos(hwnd, insertAfter, x, y, width, height, flags);

    public void ShowWindow(nint hwnd, int command) =>
        _ = NativeMethods.ShowWindow(hwnd, command);
}

/// <summary>
/// The lyric surface over the taskbar: a borderless always-on-top tool window
/// that never activates. XAML content is supplied by the app layer; this host
/// owns window styles, placements, and dispatcher marshaling.
/// </summary>
public sealed class TaskbarLyricsWindowHost : ITaskbarLyricsWindowHost, IDisposable
{
    private readonly Window window;
    private readonly WindowStyleService styles;
    private readonly SynchronizationContext? dispatcher;

    public TaskbarLyricsWindowHost(Window window, WindowStyleService? styleService = null)
    {
        this.window = window;
        styles = styleService ?? new WindowStyleService(new Win32WindowApi());
        dispatcher = SynchronizationContext.Current;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this.window);
        styles.ApplyNonActivatingStyle(hwnd);
    }

    public bool IsVisible => Interop.NativeMethods.IsWindowVisible(
        WinRT.Interop.WindowNative.GetWindowHandle(window));

    public void Show(TaskbarPlacement placement)
    {
        if (dispatcher is { } context && context != SynchronizationContext.Current)
        {
            context.Post(_ => ShowCore(placement), null);
            return;
        }

        ShowCore(placement);
    }

    public void Hide()
    {
        if (dispatcher is { } context && context != SynchronizationContext.Current)
        {
            context.Post(_ => HideCore(), null);
            return;
        }

        HideCore();
    }

    private void ShowCore(TaskbarPlacement placement)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        styles.ApplyNonActivatingStyle(hwnd);
        styles.ShowAt(hwnd, placement);
    }

    private void HideCore()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        styles.Hide(hwnd);
    }

    public void Dispose()
    {
        HideCore();
    }
}
