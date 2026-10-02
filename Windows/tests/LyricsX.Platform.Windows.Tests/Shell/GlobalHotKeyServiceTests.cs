using LyricsX.Platform.Windows.Shell;

namespace LyricsX.Platform.Windows.Tests.Shell;

[TestClass]
public sealed class GlobalHotKeyServiceTests
{
    [TestMethod]
    public void TryRegister_Success_ReturnsTrue()
    {
        using var native = new FakeHotKeyNativeApi();
        using var service = new GlobalHotKeyService(native, hotKeyIdBase: 0x5137);

        Assert.IsTrue(service.TryRegister(1, HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L'));
        Assert.AreEqual(1, native.RegisterCalls.Count);
    }

    [TestMethod]
    public void TryRegister_Conflict_ReturnsFalseAndDoesNotThrow()
    {
        using var native = new FakeHotKeyNativeApi { FailAfter = 0 };
        using var service = new GlobalHotKeyService(native, hotKeyIdBase: 0x5137);

        Assert.IsFalse(service.TryRegister(1, HotKeyModifiers.Control, 'K'));
    }

    [TestMethod]
    public void Unregister_RemovesRegistration()
    {
        using var native = new FakeHotKeyNativeApi();
        var service = new GlobalHotKeyService(native, hotKeyIdBase: 0x5137);
        service.TryRegister(1, HotKeyModifiers.Control, 'L');

        service.Unregister(1);

        Assert.AreEqual(1, native.UnregisterCalls.Count);
    }

    [TestMethod]
    public void Dispose_UnregistersEverything()
    {
        var native = new FakeHotKeyNativeApi();
        var service = new GlobalHotKeyService(native, hotKeyIdBase: 0x5137);
        service.TryRegister(1, HotKeyModifiers.Control, 'L');
        service.TryRegister(2, HotKeyModifiers.Windows, 'J');

        service.Dispose();

        Assert.AreEqual(2, native.UnregisterCalls.Count);
    }

    [TestMethod]
    public void Pressed_RaisedWithRegistrationId()
    {
        using var native = new FakeHotKeyNativeApi();
        using var service = new GlobalHotKeyService(native, hotKeyIdBase: 0x5137);
        service.TryRegister(7, HotKeyModifiers.Control, 'L');

        int? received = null;
        service.Pressed += (_, id) => received = id;
        native.RaiseHotKey(0x5137 + 7);

        Assert.AreEqual(7, received);
    }
}

internal sealed class FakeHotKeyNativeApi : IHotKeyNativeApi
{
    public int FailAfter { get; set; } = int.MaxValue;

    public List<(nint Window, int Id, uint Modifiers, uint Key)> RegisterCalls { get; } = [];

    public List<int> UnregisterCalls { get; } = [];

    private readonly Dictionary<int, nint> registered = [];

    public bool Register(nint window, int id, uint modifiers, uint key)
    {
        RegisterCalls.Add((window, id, modifiers, key));
        if (RegisterCalls.Count > FailAfter)
        {
            return false;
        }

        registered[id] = window;
        return true;
    }

    public bool Unregister(nint window, int id)
    {
        UnregisterCalls.Add(id);
        return registered.Remove(id);
    }

    public event Action<int>? HotKeyReceived;

    public void RaiseHotKey(int id) => HotKeyReceived?.Invoke(id);

    public void Dispose()
    {
    }
}
