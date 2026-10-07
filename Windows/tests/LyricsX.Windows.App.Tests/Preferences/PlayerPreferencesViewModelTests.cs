using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class PlayerPreferencesViewModelTests
{
    private static (PlayerPreferencesViewModel ViewModel, AppSettingsAccessor Accessor) Create(
        AppSettings? initial = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "player-prefs-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var accessor = new AppSettingsAccessor(
            store, initial ?? store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
        return (new PlayerPreferencesViewModel(accessor), accessor);
    }

    [TestMethod]
    public async Task Apply_PersistsPreferredSource()
    {
        var (viewModel, accessor) = Create();
        viewModel.SelectedSourceId = "apple.music";

        Assert.IsTrue(await viewModel.ApplyAsync());
        Assert.AreEqual("apple.music", accessor.Current.PreferredPlayerSourceId);
    }

    [TestMethod]
    public async Task Apply_EmptySelectionMeansAutomatic()
    {
        var (viewModel, accessor) = Create(
            AppSettings.Default with { PreferredPlayerSourceId = "pinned.app" });
        viewModel.SelectedSourceId = PlayerPreferencesViewModel.AutomaticSourceId;

        Assert.IsTrue(await viewModel.ApplyAsync());
        Assert.AreEqual(string.Empty, accessor.Current.PreferredPlayerSourceId);
    }

    [TestMethod]
    public void UpdateKnownSources_MergesDistinctAndKeepsPinned()
    {
        var (viewModel, _) = Create(
            AppSettings.Default with { PreferredPlayerSourceId = "pinned.app" });

        viewModel.UpdateKnownSources(["a.app", "A.APP", "b.app"]);

        CollectionAssert.AreEquivalent(
            new[] { "pinned.app", "a.app", "b.app" },
            viewModel.KnownSources.ToArray());
    }

    [TestMethod]
    public void Constructor_AutomaticByDefault()
    {
        var (viewModel, _) = Create();

        Assert.AreEqual(string.Empty, viewModel.SelectedSourceId);
    }
}
