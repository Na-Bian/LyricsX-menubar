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
    private const int NOTIFYICON_VERSION_4 = 4;
    private const uint NIF_MESSAGE = 0x0001;
    private const uint NIF_ICON = 0x0002;
    private const uint NIF_TIP = 0x0004;
    private const uint NIF_GUID = 0x0080;

    private readonly Guid iconGuid = Guid.NewGuid();
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
        data.uVersion = NOTIFYICON_VERSION_4;
        if (!NativeMethods.ShellNotifyIcon(NIM_ADD, ref data))
        {
            throw new Win32Exception("Shell_NotifyIcon NIM_ADD failed");
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
                case 0x0000 or 0x0002: // NIN_SELECT or WM_LBUTTONUP: open preferences
                    OpenPreferencesRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case 0x0005 or 0x0086: // WM_XBUTTONUP or WM_CONTEXTMENU: exit path
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
