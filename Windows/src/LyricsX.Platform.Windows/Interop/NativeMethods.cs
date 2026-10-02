using System.Runtime.InteropServices;

namespace LyricsX.Platform.Windows.Interop;

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
