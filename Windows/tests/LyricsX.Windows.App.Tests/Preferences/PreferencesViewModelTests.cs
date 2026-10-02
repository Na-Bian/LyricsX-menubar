using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class PreferencesViewModelTests
{
    private static (PreferencesViewModel ViewModel, AppSettingsAccessor Accessor) Create(
        AppSettings? initial = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "prefs-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var accessor = new AppSettingsAccessor(
            store, initial ?? store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
        return (new PreferencesViewModel(accessor), accessor);
    }

    [TestMethod]
    public async Task ApplyAsync_PersistsChanges()
    {
        var (viewModel, accessor) = Create();
        viewModel.WidthDip = 300;

        Assert.IsTrue(await viewModel.ApplyAsync());
        Assert.AreEqual(300, accessor.Current.WidthDip);
    }

    [TestMethod]
    public void Width_Clamped80To360()
    {
        var (viewModel, _) = Create();
        viewModel.WidthDip = 40;
        Assert.IsFalse(viewModel.Validate());
        viewModel.WidthDip = 500;
        Assert.IsFalse(viewModel.Validate());
        viewModel.WidthDip = 260;
        Assert.IsTrue(viewModel.Validate());
    }

    [TestMethod]
    public void Offset_ClampedPlusMinus30Seconds()
    {
        var (viewModel, _) = Create();
        viewModel.UserOffsetMilliseconds = 31_000;
        Assert.IsFalse(viewModel.Validate());
        viewModel.UserOffsetMilliseconds = -30_000;
        Assert.IsTrue(viewModel.Validate());
    }

    [TestMethod]
    public async Task InvalidApply_DoesNotPersistAndKeepsEdits()
    {
        var (viewModel, accessor) = Create();
        viewModel.WidthDip = 999;

        Assert.IsFalse(await viewModel.ApplyAsync());
        Assert.AreEqual(260, accessor.Current.WidthDip);
        Assert.AreEqual(999, viewModel.WidthDip); // edits retained
        Assert.IsNotNull(viewModel.ValidationError);
    }

    [TestMethod]
    public void ResetToDefaults_RestoresDefaults()
    {
        var (viewModel, _) = Create();
        viewModel.WidthDip = 320;
        viewModel.ResetToDefaults();

        Assert.AreEqual(260, viewModel.WidthDip);
        Assert.IsTrue(viewModel.Validate());
    }

    [TestMethod]
    public async Task StartupError_ShownWithoutLosingOtherSettings()
    {
        var (viewModel, accessor) = Create();
        viewModel.WidthDip = 300;
        viewModel.StartupError = "无法写入启动项";

        Assert.IsTrue(await viewModel.ApplyAsync());
        Assert.AreEqual("无法写入启动项", viewModel.StartupError);
        Assert.AreEqual(300, accessor.Current.WidthDip);
    }
}
