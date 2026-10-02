using System.ComponentModel;
using System.Runtime.InteropServices;
using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>Native taskbar geometry seam (unit tests inject fakes).</summary>
public interface ITaskbarNativeApi
{
    nint FindPrimaryTaskbar();

    PixelRect GetWindowRect(nint window);

    TaskbarEdge GetEdge(nint window);

    bool IsAutoHideEnabled();

    double GetRasterizationScale(nint window);

    bool IsForegroundFullscreen(PixelRect monitorBounds);
}

/// <summary>UI Automation seam for occupied taskbar controls.</summary>
public interface ITaskbarAutomationApi
{
    IReadOnlyList<PixelRect> GetVisibleInteractiveBounds(nint taskbarWindow);
}

/// <summary>Full-screen foreground detection seam.</summary>
public interface IFullscreenDetector
{
    bool IsForegroundFullscreen(PixelRect monitorBounds);
}

/// <summary>
/// Real Win32 geometry: FindWindow + GetWindowRect + SHAppBarMessage for edge
/// and auto-hide state, GetDpiForWindow for rasterization scale.
/// </summary>
public sealed partial class TaskbarNativeApi : ITaskbarNativeApi
{
    private const uint ABM_GETTASKBARPOS = 0x0005;
    private const uint ABM_GETSTATE = 0x0004;
    private const int ABS_AUTOHIDE = 0x0001;

    public nint FindPrimaryTaskbar() =>
        NativeMethods.FindWindow("Shell_TrayWnd", null);

    public PixelRect GetWindowRect(nint window)
    {
        if (window == nint.Zero ||
            !NativeMethods.GetWindowRect(window, out NativeMethods.RECT rect))
        {
            return default;
        }

        return new PixelRect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public TaskbarEdge GetEdge(nint window)
    {
        var data = AppBarData(window);
        _ = NativeMethods.SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
        return data.Edge switch
        {
            0 => TaskbarEdge.Left,
            1 => TaskbarEdge.Right,
            2 => TaskbarEdge.Top,
            3 => TaskbarEdge.Bottom,
            _ => InferEdge(window)
        };
    }

    public bool IsAutoHideEnabled()
    {
        var data = AppBarData(nint.Zero);
        return (NativeMethods.SHAppBarMessage(ABM_GETSTATE, ref data).ToInt64() & ABS_AUTOHIDE) != 0;
    }

    public double GetRasterizationScale(nint window) => window == nint.Zero
        ? 1.0
        : NativeMethods.GetDpiForWindow(window) / 96.0;

    public bool IsForegroundFullscreen(PixelRect monitorBounds)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero ||
            !NativeMethods.GetWindowRect(foreground, out NativeMethods.RECT rect))
        {
            return false;
        }

        return rect.Left <= monitorBounds.X &&
            rect.Top <= monitorBounds.Y &&
            rect.Right >= monitorBounds.Right &&
            rect.Bottom >= monitorBounds.Bottom;
    }

    private static TaskbarEdge InferEdge(nint window)
    {
        var rect = GetWindowRectStatic(window);
        if (rect.Width == 0 || rect.Height == 0)
        {
            return TaskbarEdge.Bottom;
        }

        if (rect.Width > rect.Height)
        {
            return rect.Y == 0 ? TaskbarEdge.Top : TaskbarEdge.Bottom;
        }

        return rect.X == 0 ? TaskbarEdge.Left : TaskbarEdge.Right;
    }

    private static PixelRect GetWindowRectStatic(nint window)
    {
        if (window == nint.Zero ||
            !NativeMethods.GetWindowRect(window, out NativeMethods.RECT rect))
        {
            return default;
        }

        return new PixelRect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private static NativeMethods.APPBARDATA AppBarData(nint window) => new()
    {
        CallbackSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        WindowHandle = window
    };

}

/// <summary>
/// SHQueryUserNotificationState-based full-screen detection: QUIET/APP states
/// mean a full-screen app owns the foreground.
/// </summary>
public sealed class ShellFullscreenDetector : IFullscreenDetector
{
    public bool IsForegroundFullscreen(PixelRect monitorBounds) =>
        Interop.NativeMethods.QueryUserNotificationState() is
            Interop.UserNotificationState.QUIET or
            Interop.UserNotificationState.APP;
}
