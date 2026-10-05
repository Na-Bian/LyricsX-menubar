using System.ComponentModel;
using System.Runtime.InteropServices;
using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Shell;

public interface ITrayIconService : IDisposable
{
    event EventHandler? OpenPreferencesRequested;

    event EventHandler? ExitRequested;

    void Show();
}

/// <summary>
/// Tray icon via Shell_NotifyIconW with a stable GUID and a hidden message
/// window; re-adds itself after Explorer restarts (TaskbarCreated).
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private const int NIM_ADD = 0x0000;
    private const int NIM_MODIFY = 0x0001;
    private const int NIM_DELETE = 0x0002;
    private const int NIM_SETVERSION = 0x0004;
    private const int NOTIFYICON_VERSION_4 = 4;
    private const uint NIF_MESSAGE = 0x0001;
    private const uint NIF_ICON = 0x0002;
    private const uint NIF_TIP = 0x0004;
    private const uint NIF_GUID = 0x0080;

    // NOTIFYICON_VERSION_4 notification events (LOWORD of lParam)
    private const ushort NIN_SELECT_V4 = 0x0400;
    private const ushort WM_LBUTTONUP_V4 = 0x0202;
    private const ushort WM_CONTEXTMENU_V4 = 0x007B;

    // stable per-install GUID: survives Explorer restarts so the shell can
    // reuse/clean the same icon slot
    private static readonly Guid iconGuid =
        new("2E5A9D3B-7C42-4F6E-9D18-A53B7F2C8E01");
    private readonly nint messageWindow;
    private uint taskbarCreatedMessage;
    private bool visible;

    public event EventHandler? OpenPreferencesRequested;

    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        messageWindow = TrayMessageWindow.Create(OnTrayMessage);
        taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    public void Show()
    {
        var data = BuildNotifyIconData();
        // a stable GUID means a previous run's icon entry may still exist;
        // NIM_MODIFY re-attaches to it instead of failing
        if (!NativeMethods.ShellNotifyIcon(NIM_ADD, ref data) &&
            !NativeMethods.ShellNotifyIcon(NIM_MODIFY, ref data))
        {
            throw new Win32Exception("Shell_NotifyIcon NIM_ADD failed");
        }

        data.uVersion = NOTIFYICON_VERSION_4;
        if (!NativeMethods.ShellNotifyIcon(NIM_SETVERSION, ref data))
        {
            // version 4 callback format unavailable: keep the icon, events
            // fall back to no-op rather than misfiring
        }

        visible = true;
    }

    public void Dispose()
    {
        if (visible)
        {
            var data = BuildNotifyIconData();
            _ = NativeMethods.ShellNotifyIcon(NIM_DELETE, ref data);
            visible = false;
        }

        TrayMessageWindow.Destroy(messageWindow);
    }

    private nint OnTrayMessage(nint hwnd, int message, nint wParam, nint lParam)
    {
        if (taskbarCreatedMessage != 0 && message == (int)taskbarCreatedMessage && visible)
        {
            // Explorer restarted: re-add the icon
            var data = BuildNotifyIconData();
            _ = NativeMethods.ShellNotifyIcon(NIM_ADD, ref data);
            return nint.Zero;
        }

        if (message == 0x8F00) // tray callback
        {
            // NOTIFYICON_VERSION_4: LOWORD(lParam) is the notification event
            var notification = (ushort)(lParam.ToInt64() & 0xFFFF);
            switch (notification)
            {
                case NIN_SELECT_V4 or WM_LBUTTONUP_V4:
                    OpenPreferencesRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_CONTEXTMENU_V4:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        return nint.Zero;
    }

    private NativeMethods.NOTIFYICONDATA BuildNotifyIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = messageWindow,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_TIP | NIF_GUID,
        uCallbackMessage = 0x8F00, // WM_APP-range tray callback
        hIcon = NativeMethods.LoadIcon(nint.Zero, NativeMethods.IDI_APPLICATION),
        guidItem = iconGuid,
        szTip = "LyricsX"
    };
}

internal static class TrayMessageWindow
{
    private static readonly Dictionary<nint, Func<nint, int, nint, nint, nint>> callbacks = [];
    private static readonly object gate = new();
    private static ushort classAtom;
    private static WndProcDelegate? procRoot;

    private delegate nint WndProcDelegate(nint hwnd, int message, nint wParam, nint lParam);

    public static nint Create(Func<nint, int, nint, nint, nint> callback)
    {
        lock (gate)
        {
            EnsureClass();
            var hwnd = NativeMethods.CreateWindowEx(
                0, new nint(classAtom), string.Empty, 0, 0, 0, 0, 0,
                new nint(-3), nint.Zero, nint.Zero, nint.Zero);
            if (hwnd != nint.Zero)
            {
                callbacks[hwnd] = callback;
            }

            return hwnd;
        }
    }

    public static void Destroy(nint hwnd)
    {
        lock (gate)
        {
            if (callbacks.Remove(hwnd))
            {
                NativeMethods.DestroyWindow(hwnd);
            }
        }
    }

    private static void EnsureClass()
    {
        if (classAtom != 0)
        {
            return;
        }

        procRoot = WndProc;
        var wndClass = new NativeMethods.WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(procRoot),
            lpszClassName = "LyricsX-TrayWindow"
        };
        classAtom = NativeMethods.RegisterClass(ref wndClass);
    }

    private static nint WndProc(nint hwnd, int message, nint wParam, nint lParam)
    {
        if (callbacks.TryGetValue(hwnd, out var callback))
        {
            return callback(hwnd, message, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
