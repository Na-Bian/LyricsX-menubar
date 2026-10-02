using System.Runtime.InteropServices;
using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Shell;

[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

/// <summary>Win32 hotkey seam (unit tests inject fakes).</summary>
public interface IHotKeyNativeApi : IDisposable
{
    bool Register(nint window, int id, uint modifiers, uint virtualKey);

    bool Unregister(nint window, int id);

    /// <summary>Raised when the OS (or a test fake) delivers a hotkey for the
    /// full registration id.</summary>
    event Action<int>? HotKeyReceived;
}

public interface IGlobalHotKeyService : IDisposable
{
    bool TryRegister(int id, HotKeyModifiers modifiers, uint virtualKey);

    void Unregister(int id);

    event EventHandler<int>? Pressed;
}

/// <summary>
/// Global shortcuts via RegisterHotKey against a hidden message window;
/// WM_HOTKEY re-emitted as a numeric registration-id event. Conflicts
/// surface as a false return, never an exception.
/// </summary>
public sealed class GlobalHotKeyService : IGlobalHotKeyService
{
    private readonly IHotKeyNativeApi native;
    private readonly int hotKeyIdBase;
    private readonly nint messageWindow;
    private readonly Dictionary<int, nint> registered = [];

    public event EventHandler<int>? Pressed;

    public GlobalHotKeyService(IHotKeyNativeApi native, int hotKeyIdBase)
    {
        this.native = native;
        this.hotKeyIdBase = hotKeyIdBase;
        messageWindow = HotKeyMessageWindow.Create(WndProc);
        native.HotKeyReceived += OnNativeHotKey;
    }

    public bool TryRegister(int id, HotKeyModifiers modifiers, uint virtualKey)
    {
        if (!native.Register(messageWindow, hotKeyIdBase + id, (uint)modifiers, virtualKey))
        {
            return false;
        }

        registered[id] = messageWindow;
        return true;
    }

    public void Unregister(int id)
    {
        if (native.Unregister(messageWindow, hotKeyIdBase + id))
        {
            registered.Remove(id);
        }
    }

    public void Dispose()
    {
        native.HotKeyReceived -= OnNativeHotKey;
        foreach (var id in registered.Keys.ToArray())
        {
            native.Unregister(messageWindow, hotKeyIdBase + id);
        }

        registered.Clear();
        HotKeyMessageWindow.Destroy(messageWindow);
        native.Dispose();
    }

    private void OnNativeHotKey(int fullId) =>
        Pressed?.Invoke(this, fullId - hotKeyIdBase);

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam)
    {
        const int WM_HOTKEY = 0x0312;
        if (message == WM_HOTKEY)
        {
            OnNativeHotKey(wParam.ToInt32());
            return nint.Zero;
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }
}

/// <summary>Hidden message-only window hosting RegisterHotKey callbacks.</summary>
internal static class HotKeyMessageWindow
{
    private const int WM_HOTKEY = 0x0312;

    private static readonly Dictionary<nint, Func<nint, int, nint, nint, nint>> callbacks = [];
    private static readonly object gate = new();
    private static ushort classAtom;
    private static WndProcDelegate? procRoot; // rooted so the GC keeps the thunk

    private delegate nint WndProcDelegate(nint hwnd, int message, nint wParam, nint lParam);

    private static readonly nint HwndMessage = new(-3);

    public static nint Create(Func<nint, int, nint, nint, nint> callback)
    {
        lock (gate)
        {
            EnsureClass();
            var hwnd = NativeMethods.CreateWindowEx(
                0, new nint(classAtom), string.Empty, 0, 0, 0, 0, 0,
                HwndMessage, nint.Zero, nint.Zero, nint.Zero);
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
            lpszClassName = "LyricsX-HotKeyWindow"
        };
        classAtom = NativeMethods.RegisterClass(ref wndClass);
    }

    private static nint WndProc(nint hwnd, int message, nint wParam, nint lParam)
    {
        if (message == WM_HOTKEY &&
            callbacks.TryGetValue(hwnd, out var callback))
        {
            return callback(hwnd, message, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
