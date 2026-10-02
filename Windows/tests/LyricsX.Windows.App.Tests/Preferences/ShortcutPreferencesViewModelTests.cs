using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;
using LyricsX.Platform.Windows.Shell;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class ShortcutPreferencesViewModelTests
{
    [TestMethod]
    public void Validate_RejectsBareLetterAndReservedCombos()
    {
        var (viewModel, _, _) = Create();

        Assert.IsFalse(viewModel.ValidateBinding(HotKeyModifiers.None, 'L'));
        Assert.IsFalse(viewModel.ValidateBinding(HotKeyModifiers.Windows, 'D')); // Win+D reserved
        Assert.IsTrue(viewModel.ValidateBinding(HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L'));
    }

    [TestMethod]
    public async Task Apply_RegistersThenPersists()
    {
        var native = new FakeHotKeyNativeApi();
        var (viewModel, accessor, _) = Create(native);
        viewModel.SetBinding("toggle-visibility", HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L');

        Assert.IsTrue(await viewModel.ApplyAsync());
        Assert.AreEqual(1, native.RegisterCalls.Count);
        Assert.AreEqual(HotKeyModifiers.Control | HotKeyModifiers.Alt,
            (HotKeyModifiers)native.RegisterCalls[0].Modifiers);
    }

    [TestMethod]
    public async Task Cancel_RestoresPriorRegistrations()
    {
        var native = new FakeHotKeyNativeApi();
        var (viewModel, accessor, _) = Create(native);
        viewModel.SetBinding("toggle-visibility", HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L');
        await viewModel.ApplyAsync();
        viewModel.SetBinding("toggle-visibility", HotKeyModifiers.Control | HotKeyModifiers.Shift, 'K');

        viewModel.Cancel();

        Assert.AreEqual(1, native.RegisterCalls.Count); // no new registration
        Assert.AreEqual(HotKeyModifiers.Control | HotKeyModifiers.Alt,
            (HotKeyModifiers)native.RegisterCalls[0].Modifiers);
    }

    private static (ShortcutPreferencesViewModel, AppSettingsAccessor, FakeHotKeyNativeApi) Create(
        FakeHotKeyNativeApi? native = null)
    {
        native ??= new FakeHotKeyNativeApi();
        var root = Path.Combine(Path.GetTempPath(), "sc-prefs-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var accessor = new AppSettingsAccessor(
            store, store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
        return (new ShortcutPreferencesViewModel(accessor, native), accessor, native);
    }
}

internal sealed class FakeHotKeyNativeApi : IHotKeyNativeApi
{
    public List<(nint Window, int Id, uint Modifiers, uint Key)> RegisterCalls { get; } = [];

    public List<int> UnregisterCalls { get; } = [];

    public event Action<int>? HotKeyReceived
    {
        add { }
        remove { }
    }

    public bool Register(nint window, int id, uint modifiers, uint key)
    {
        RegisterCalls.Add((window, id, modifiers, key));
        return true;
    }

    public bool Unregister(nint window, int id)
    {
        UnregisterCalls.Add(id);
        return true;
    }

    public void Dispose()
    {
    }
}
