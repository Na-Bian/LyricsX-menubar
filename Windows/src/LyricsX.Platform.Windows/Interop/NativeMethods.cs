using System.Runtime.InteropServices;

namespace LyricsX.Platform.Windows.Interop;

/// <summary>
/// SHQueryUserNotificationState values: QUIET/APP mean a full-screen app or
/// presentation owns the foreground.
/// </summary>
internal enum UserNotificationState
{
    NOT_PRESENT = 1,
    BUSY = 2,
    RUNNING_DLL = 3,
    QUIET = 4,
    ACCEPTS_NOTIFICATIONS = 5,
    APP = 7
}

/// <summary>
/// All Win32 P/Invoke entry points for the platform layer. Declared with
/// [LibraryImport] (source-generated marshaling), blittable structures, and
/// SetLastError = true so failures surface as Win32Exception.
/// </summary>
internal static partial class NativeMethods
{
    // user32

    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TOOLWINDOW = 0x00000080;
    internal const long WS_EX_APPWINDOW = 0x00040000;
    internal const long WS_EX_NOACTIVATE = 0x08000000;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const uint SWP_HIDEWINDOW = 0x0080;
    internal const nint HWND_TOPMOST = -1;

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint FindWindow(string className, string? windowName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out RECT rect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial uint RegisterWindowMessage(string message);

    // shell32

    [LibraryImport("shell32.dll", EntryPoint = "SHAppBarMessage", SetLastError = true)]
    internal static partial nint SHAppBarMessage(uint message, ref APPBARDATA data);

    [LibraryImport("shell32.dll", EntryPoint = "SHQueryUserNotificationState")]
    internal static partial UserNotificationState QueryUserNotificationState();

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    // dwmapi

    // (entry points added by the windowing tasks)

    // kernel32

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial void SecureZeroMemory(nint destination, nuint length);

    // advapi32

    // (credential entry points added by the shell task)

    // credui

    // (entry points added by the shell task)

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public int CallbackSize;
        public nint WindowHandle;
        public uint CallbackMessage;
        public uint Edge;
        public RECT Rect;
        public int lParam;
    }
}
