using System.Runtime.InteropServices;

namespace LyricsX.Platform.Windows.Interop;

/// <summary>
/// SafeHandle wrappers for native allocations owned by the platform layer:
/// credentials read from Windows Credential Manager and HGLOBAL registry
/// buffers are released (and where required zeroed) deterministically.
/// </summary>
internal sealed class LocalAllocBuffer : SafeHandle
{
    public LocalAllocBuffer(int byteCount) : base(nint.Zero, ownsHandle: true)
    {
        SetHandle(Marshal.AllocHGlobal(byteCount));
    }

    public override bool IsInvalid => handle == nint.Zero;

    public nint Pointer => handle;

    protected override bool ReleaseHandle()
    {
        Marshal.FreeHGlobal(handle);
        return true;
    }
}

internal sealed class ZeroableBuffer(nint pointer, int byteCount) : SafeHandle(nint.Zero, ownsHandle: true)
{
    public override bool IsInvalid => pointer == nint.Zero;

    public nint Pointer => pointer;

    public int ByteCount { get; } = byteCount;

    protected override bool ReleaseHandle()
    {
        NativeMethods.SecureZeroMemory(pointer, (nuint)ByteCount);
        Marshal.FreeHGlobal(pointer);
        return true;
    }
}
