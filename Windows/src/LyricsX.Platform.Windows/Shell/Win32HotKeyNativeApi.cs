using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Shell;

/// <summary>Real RegisterHotKey/UnregisterHotKey adapter. WM_HOTKEY reaches
/// the service through the message-window proc; the event remains unused.</summary>
public sealed class Win32HotKeyNativeApi : IHotKeyNativeApi
{
    public event Action<int>? HotKeyReceived
    {
        add { }
        remove { }
    }

    public bool Register(nint window, int id, uint modifiers, uint virtualKey) =>
        NativeMethods.RegisterHotKey(window, id, modifiers, virtualKey);

    public bool Unregister(nint window, int id) =>
        NativeMethods.UnregisterHotKey(window, id);

    public void Dispose()
    {
    }
}
