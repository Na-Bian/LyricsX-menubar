using System.Runtime.InteropServices;

namespace LyricsX.Windows.App;

internal static class NativeMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint window, string text, string caption, uint type);

    public static void Show(string caption, string text) =>
        _ = MessageBoxW(nint.Zero, text, caption, 0x10); // MB_ICONERROR
}
